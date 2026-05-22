using System.Threading.RateLimiting;

namespace SCP.StorageFSC.Services
{
    public sealed class FileTransferLimiter : IAsyncDisposable
    {
        private readonly object _sync = new();
        private readonly List<LimiterBundle> _retiredBundles = [];
        private LimiterBundle _current;

        public FileTransferLimiter(
            int maxConcurrentUploads,
            int maxConcurrentDownloads,
            long uploadBytesPerSecond,
            long downloadBytesPerSecond)
        {
            _current = CreateBundle(new FileTransferLimitSettings(
                maxConcurrentUploads,
                maxConcurrentDownloads,
                uploadBytesPerSecond,
                downloadBytesPerSecond));
        }

        public FileTransferLimitSettings CurrentSettings => Volatile.Read(ref _current).Settings;

        public void Update(FileTransferLimitSettings settings)
        {
            var next = CreateBundle(settings);

            lock (_sync)
            {
                var previous = _current;
                Volatile.Write(ref _current, next);
                _retiredBundles.Add(previous);
            }
        }

        public async ValueTask<IAsyncDisposable> AcquireUploadSlotAsync(CancellationToken cancellationToken)
        {
            var bundle = Volatile.Read(ref _current);
            var lease = await bundle.UploadConcurrency.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
                throw new IOException("Too many concurrent uploads.");

            return new FileTransferSlot(lease);
        }

        public async ValueTask<IAsyncDisposable> AcquireDownloadSlotAsync(CancellationToken cancellationToken)
        {
            var bundle = Volatile.Read(ref _current);
            var lease = await bundle.DownloadConcurrency.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
                throw new IOException("Too many concurrent downloads.");

            return new FileTransferSlot(lease);
        }

        public ValueTask WaitUploadSpeedAsync(int bytes, CancellationToken cancellationToken)
        {
            var bundle = Volatile.Read(ref _current);
            return WaitSpeedAsync(
                bundle.UploadSpeed,
                bytes,
                bundle.Settings.UploadBytesPerSecondInt,
                "Upload speed limiter queue is full.",
                cancellationToken);
        }

        public ValueTask WaitDownloadSpeedAsync(int bytes, CancellationToken cancellationToken)
        {
            var bundle = Volatile.Read(ref _current);
            return WaitSpeedAsync(
                bundle.DownloadSpeed,
                bytes,
                bundle.Settings.DownloadBytesPerSecondInt,
                "Download speed limiter queue is full.",
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            List<LimiterBundle> bundles;

            lock (_sync)
            {
                bundles = [.. _retiredBundles, _current];
                _retiredBundles.Clear();
            }

            foreach (var bundle in bundles)
            {
                await bundle.DisposeAsync();
            }
        }

        private static LimiterBundle CreateBundle(FileTransferLimitSettings settings)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaxConcurrentUploads);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaxConcurrentDownloads);

            _ = settings.UploadBytesPerSecondInt;
            _ = settings.DownloadBytesPerSecondInt;

            return new LimiterBundle(
                settings,
                new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = settings.MaxConcurrentUploads,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }),
                new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = settings.MaxConcurrentDownloads,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }),
                CreateSpeedLimiter(settings.UploadBytesPerSecondInt),
                CreateSpeedLimiter(settings.DownloadBytesPerSecondInt));
        }

        private static TokenBucketRateLimiter CreateSpeedLimiter(int bytesPerSecond)
        {
            return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = bytesPerSecond,
                TokensPerPeriod = bytesPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = CalculateQueueLimit(bytesPerSecond),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        private static async ValueTask WaitSpeedAsync(
            TokenBucketRateLimiter limiter,
            int bytes,
            int bytesPerSecond,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);

            var remaining = bytes;
            while (remaining > 0)
            {
                var permitCount = Math.Min(remaining, bytesPerSecond);
                using var lease = await limiter.AcquireAsync(permitCount, cancellationToken);

                if (!lease.IsAcquired)
                    throw new IOException(errorMessage);

                remaining -= permitCount;
            }
        }

        private static int CalculateQueueLimit(int bytesPerSecond)
        {
            const int maxQueuedSeconds = 60;
            var queueLimit = (long)bytesPerSecond * maxQueuedSeconds;
            return queueLimit > int.MaxValue ? int.MaxValue : (int)queueLimit;
        }

        private sealed class FileTransferSlot : IAsyncDisposable
        {
            private readonly RateLimitLease _lease;
            private bool _disposed;

            public FileTransferSlot(RateLimitLease lease)
            {
                _lease = lease;
            }

            public ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _lease.Dispose();
                    _disposed = true;
                }

                return ValueTask.CompletedTask;
            }
        }

        private sealed class LimiterBundle : IAsyncDisposable
        {
            public LimiterBundle(
                FileTransferLimitSettings settings,
                ConcurrencyLimiter uploadConcurrency,
                ConcurrencyLimiter downloadConcurrency,
                TokenBucketRateLimiter uploadSpeed,
                TokenBucketRateLimiter downloadSpeed)
            {
                Settings = settings;
                UploadConcurrency = uploadConcurrency;
                DownloadConcurrency = downloadConcurrency;
                UploadSpeed = uploadSpeed;
                DownloadSpeed = downloadSpeed;
            }

            public FileTransferLimitSettings Settings { get; }
            public ConcurrencyLimiter UploadConcurrency { get; }
            public ConcurrencyLimiter DownloadConcurrency { get; }
            public TokenBucketRateLimiter UploadSpeed { get; }
            public TokenBucketRateLimiter DownloadSpeed { get; }

            public async ValueTask DisposeAsync()
            {
                await UploadConcurrency.DisposeAsync();
                await DownloadConcurrency.DisposeAsync();
                await UploadSpeed.DisposeAsync();
                await DownloadSpeed.DisposeAsync();
            }
        }
    }

    public sealed record FileTransferLimitSettings(
        int MaxConcurrentUploads,
        int MaxConcurrentDownloads,
        long UploadBytesPerSecond,
        long DownloadBytesPerSecond)
    {
        internal int UploadBytesPerSecondInt => ToRateLimit(UploadBytesPerSecond, nameof(UploadBytesPerSecond));
        internal int DownloadBytesPerSecondInt => ToRateLimit(DownloadBytesPerSecond, nameof(DownloadBytesPerSecond));

        private static int ToRateLimit(long bytesPerSecond, string parameterName)
        {
            if (bytesPerSecond <= 0 || bytesPerSecond > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Rate limit must be between 1 and {int.MaxValue} bytes per second.");
            }

            return (int)bytesPerSecond;
        }
    }
}
