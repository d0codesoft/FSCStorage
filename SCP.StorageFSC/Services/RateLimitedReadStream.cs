namespace SCP.StorageFSC.Services
{
    internal sealed class RateLimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly FileTransferLimiter _limiter;
        private readonly IAsyncDisposable _slot;
        private bool _disposed;

        public RateLimitedReadStream(
            Stream inner,
            FileTransferLimiter limiter,
            IAsyncDisposable slot)
        {
            _inner = inner;
            _limiter = limiter;
            _slot = slot;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read > 0)
                _limiter.WaitDownloadSpeedAsync(read, CancellationToken.None).AsTask().GetAwaiter().GetResult();

            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            if (read > 0)
                await _limiter.WaitDownloadSpeedAsync(read, cancellationToken);

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    _slot.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _inner.DisposeAsync();
                await _slot.DisposeAsync();
                _disposed = true;
            }

            await base.DisposeAsync();
        }
    }
}
