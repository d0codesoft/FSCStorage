using scp_fs_cli.Infrastructure;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace scp_fs_cli.Services
{
    public sealed class FileUploadService
    {
        private const long SimpleUploadThresholdBytes = 200L * 1024 * 1024;
        private const long MinMultipartPartSizeBytes = 5L * 1024 * 1024;
        private const long MaxMultipartPartSizeBytes = 100L * 1024 * 1024;
        private static readonly TimeSpan MultipartCompletionPollDelay = TimeSpan.FromSeconds(2);

        private readonly ClientCliClientFactory _clientFactory;

        public FileUploadService(ClientCliClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task UploadAsync(
            FileInfo fileInfo,
            int threads,
            int retries,
            string? configPath,
            string? category,
            string? externalKey,
            CancellationToken cancellationToken = default)
        {
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"File not found: {fileInfo.FullName}");

            if (threads <= 0)
                throw new ArgumentOutOfRangeException(nameof(threads), "Thread count must be greater than 0.");

            if (retries < 0)
                throw new ArgumentOutOfRangeException(nameof(retries), "Retry count cannot be negative.");

            using var client = await _clientFactory.CreateAsync(configPath, cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"Service: {client.ServiceUrl}");
            Console.WriteLine($"File: {fileInfo.FullName}");
            Console.WriteLine($"Size: {SizeFormatter.Format(fileInfo.Length)} : {fileInfo.Length} bytes");

            UploadResult result;
            if (fileInfo.Length < SimpleUploadThresholdBytes)
            {
                Console.WriteLine("Mode: simple upload");
                result = await UploadWholeFileAsync(client, fileInfo, category, externalKey, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var effectiveThreads = Math.Max(1, threads);
                var partSize = CalculateOptimalPartSize(fileInfo.Length, effectiveThreads);

                Console.WriteLine("Mode: multipart upload");
                Console.WriteLine($"Threads: {effectiveThreads}");
                Console.WriteLine($"Part size: {SizeFormatter.Format(partSize)}");

                result = await UploadMultipartAsync(
                    client,
                    fileInfo,
                    effectiveThreads,
                    partSize,
                    retries,
                    cancellationToken).ConfigureAwait(false);
            }

            Console.WriteLine("Upload finished.");
            Console.WriteLine(result.ToDisplayString());
        }

        private static async Task<UploadResult> UploadWholeFileAsync(
            FscFileStorageClient client,
            FileInfo fileInfo,
            string? category,
            string? externalKey,
            CancellationToken cancellationToken)
        {
            using var progress = new ProgressReporter("Uploading", fileInfo.Length);
            var saveResult = await client.UploadWholeFileAsync(
                fileInfo,
                category,
                externalKey,
                progress.Report,
                cancellationToken).ConfigureAwait(false);

            progress.Complete();

            return new UploadResult(
                Mode: "simple",
                FileGuid: saveResult.File?.FileGuid,
                UploadId: null,
                Status: saveResult.Status.ToString(),
                ResponseText: saveResult.ToDisplayJson());
        }

        private static async Task<UploadResult> UploadMultipartAsync(
            FscFileStorageClient client,
            FileInfo fileInfo,
            int threads,
            long partSize,
            int retries,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("Calculating SHA-256...");
            string fullFileSha256;
            using (var hashProgress = new ProgressReporter("Hashing", fileInfo.Length))
            {
                fullFileSha256 = await ComputeSha256Async(fileInfo, hashProgress.Report, cancellationToken).ConfigureAwait(false);
                hashProgress.Complete();
            }

            Console.WriteLine($"SHA-256: {fullFileSha256}");

            var initResult = await client.InitMultipartAsync(
                new InitMultipartUploadRequest(
                    FileName: fileInfo.Name,
                    FileSize: fileInfo.Length,
                    ContentType: ContentTypes.Get(fileInfo.Extension),
                    PartSize: partSize,
                    ExpectedChecksumSha256: fullFileSha256,
                    TenantId: client.Config.TenantId,
                    Metadata: null,
                    ExpiresAtUtc: null),
                cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"UploadId: {initResult.UploadId}");
            Console.WriteLine($"Total parts: {initResult.TotalParts}");

            var statusResult = await UploadPartsAsync(
                client,
                fileInfo,
                initResult,
                threads,
                partSize,
                retries,
                cancellationToken).ConfigureAwait(false);

            return new UploadResult(
                Mode: "multipart",
                FileGuid: null,
                UploadId: statusResult.UploadId,
                Status: statusResult.Status.ToString(),
                ResponseText: statusResult.ToDisplayJson());
        }

        private static async Task<MultipartUploadStatusResult> UploadPartsAsync(
            FscFileStorageClient client,
            FileInfo fileInfo,
            InitMultipartUploadResult initResult,
            int threads,
            long partSize,
            int retries,
            CancellationToken cancellationToken)
        {
            var nextPartNumber = 0;
            var completedParts = 0;
            var uploadTasks = new List<Task>();
            var failures = new List<Exception>();
            var syncRoot = new object();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var uploadProgress = new ProgressReporter("Uploading", fileInfo.Length);

            for (var worker = 0; worker < threads; worker++)
            {
                uploadTasks.Add(Task.Run(async () =>
                {
                    while (!linkedCancellation.IsCancellationRequested)
                    {
                        int currentPartNumber;
                        lock (syncRoot)
                        {
                            nextPartNumber++;
                            currentPartNumber = nextPartNumber;
                        }

                        if (currentPartNumber > initResult.TotalParts)
                            break;

                        try
                        {
                            await UploadSinglePartWithRetryAsync(
                                client,
                                fileInfo,
                                initResult.UploadId,
                                currentPartNumber,
                                partSize,
                                uploadProgress.Report,
                                retries,
                                linkedCancellation.Token).ConfigureAwait(false);

                            int done;
                            lock (syncRoot)
                            {
                                completedParts++;
                                done = completedParts;
                            }

                            Console.WriteLine($"Part {currentPartNumber}/{initResult.TotalParts} uploaded ({done}/{initResult.TotalParts}).");
                        }
                        catch (Exception ex)
                        {
                            lock (failures)
                                failures.Add(ex);

                            await linkedCancellation.CancelAsync().ConfigureAwait(false);
                            break;
                        }
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);

            if (failures.Count > 0)
            {
                try
                {
                    await client.AbortMultipartAsync(initResult.UploadId, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                }

                throw new AggregateException("Multipart upload failed.", failures);
            }

            uploadProgress.Complete();

            Console.WriteLine("Requesting multipart completion...");
            var completeResult = await client.CompleteMultipartAsync(initResult.UploadId, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Multipart completion operation accepted. Status: {completeResult.Status}");

            return await WaitForMultipartCompletionAsync(
                client,
                initResult.UploadId,
                MultipartCompletionPollDelay,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<MultipartUploadStatusResult> WaitForMultipartCompletionAsync(
            FscFileStorageClient client,
            Guid uploadId,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Waiting for multipart background operation. Delay: {delay.TotalSeconds:0.#} sec.");
            var printed = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var status = await client.GetMultipartStatusAsync(uploadId, cancellationToken).ConfigureAwait(false);
                var line = $"Multipart wait for server: {status.Status}. Parts: {status.TotalParts}.";
                Console.Write("\r" + line.PadRight(Math.Max(printed, line.Length)));
                printed = Math.Max(printed, line.Length);

                switch (status.Status)
                {
                    case MultipartUploadStatus.Completed:
                        Console.WriteLine();
                        Console.WriteLine("Multipart operation completed.");
                        if (!string.IsNullOrWhiteSpace(status.FinalChecksumSha256))
                            Console.WriteLine($"Final SHA-256: {status.FinalChecksumSha256}");
                        if (!string.IsNullOrWhiteSpace(status.RelativePath))
                            Console.WriteLine($"Relative path: {status.RelativePath}");
                        return status;

                    case MultipartUploadStatus.Failed:
                    case MultipartUploadStatus.Aborted:
                    case MultipartUploadStatus.Expired:
                        Console.WriteLine();
                        var reason = string.IsNullOrWhiteSpace(status.ErrorMessage)
                            ? status.Status.ToString()
                            : $"{status.Status}: {status.ErrorMessage}";
                        throw new InvalidOperationException($"Multipart operation did not complete successfully. {reason}");
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task UploadSinglePartWithRetryAsync(
            FscFileStorageClient client,
            FileInfo fileInfo,
            Guid uploadId,
            int partNumber,
            long partSize,
            Action<long> onBytesRead,
            int retries,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    await client.UploadPartAsync(
                        fileInfo,
                        uploadId,
                        partNumber,
                        partSize,
                        onBytesRead,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception) when (attempt <= retries + 1)
                {
                    if (attempt > retries)
                        throw;

                    var delay = TimeSpan.FromMilliseconds(500 * attempt);
                    Console.WriteLine($"Part {partNumber} failed on attempt {attempt}. Retrying in {delay.TotalMilliseconds:0} ms...");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<string> ComputeSha256Async(FileInfo fileInfo, Action<long> onBytesRead, CancellationToken cancellationToken)
        {
            const int BufferSize = 1024 * 1024 * 4; // 4 MB
            const long ProgressStep = 64L * 1024 * 1024; // 64 MB

            await using var stream = new FileStream(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: BufferSize,
                options: FileOptions.SequentialScan);

            using var sha256 = SHA256.Create();

            var buffer = new byte[BufferSize];
            long totalRead = 0;
            long lastProgress = 0;

            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                                       .ConfigureAwait(false);
                if (read == 0)
                    break;

                totalRead += read;

                sha256.TransformBlock(buffer, 0, read, null, 0);

                if (onBytesRead != null && totalRead - lastProgress >= ProgressStep)
                {
                    onBytesRead(totalRead - lastProgress);
                    lastProgress = totalRead;
                }

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            onBytesRead?.Invoke(totalRead);

            return Convert.ToHexString(sha256.Hash!);

            //await using var stream = new ProgressReadStream(
            //    new FileStream(
            //        fileInfo.FullName,
            //        FileMode.Open,
            //        FileAccess.Read,
            //        FileShare.Read,
            //        bufferSize: 1024 * 128,
            //        options: FileOptions.Asynchronous | FileOptions.SequentialScan),
            //    onBytesRead);

            //using var sha256 = SHA256.Create();
            //var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            //return Convert.ToHexString(hash);
        }

        private static long CalculateOptimalPartSize(long fileSize, int threadCount)
        {
            var targetPartCount = Math.Max(threadCount * 6, 12);
            var rawPartSize = (long)Math.Ceiling((double)fileSize / targetPartCount);
            var clamped = Math.Max(MinMultipartPartSizeBytes, rawPartSize);
            clamped = Math.Min(MaxMultipartPartSizeBytes, clamped);

            const long align = 1024L * 1024L;
            var aligned = ((clamped + align - 1) / align) * align;
            aligned = Math.Max(MinMultipartPartSizeBytes, aligned);
            aligned = Math.Min(MaxMultipartPartSizeBytes, aligned);

            return aligned;
        }
    }

    internal sealed record UploadResult(
        string Mode,
        Guid? FileGuid,
        Guid? UploadId,
        string? Status,
        string ResponseText)
    {
        public string ToDisplayString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Mode: {Mode}");
            if (FileGuid.HasValue)
                builder.AppendLine($"FileGuid: {FileGuid}");
            if (UploadId.HasValue)
                builder.AppendLine($"UploadId: {UploadId}");
            if (!string.IsNullOrWhiteSpace(Status))
                builder.AppendLine($"Status: {Status}");
            builder.AppendLine("Response:");
            builder.AppendLine(ResponseText);
            return builder.ToString();
        }
    }

    internal sealed class ProgressReporter : IDisposable
    {
        private readonly string _label;
        private readonly long _totalBytes;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Timer _timer;
        private long _completedBytes;
        private int _printed;

        public ProgressReporter(string label, long totalBytes)
        {
            _label = label;
            _totalBytes = Math.Max(1, totalBytes);
            _timer = new Timer(_ => PrintProgress(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        public void Report(long bytes)
        {
            Interlocked.Add(ref _completedBytes, bytes);
        }

        public void Complete()
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _stopwatch.Stop();
            Interlocked.Exchange(ref _completedBytes, _totalBytes);
            PrintProgress(final: true);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void PrintProgress(bool final = false)
        {
            var completed = Math.Min(Interlocked.Read(ref _completedBytes), _totalBytes);
            var elapsedSeconds = Math.Max(_stopwatch.Elapsed.TotalSeconds, 0.001d);
            var percent = completed * 100d / _totalBytes;
            var speedBytesPerSecond = completed / elapsedSeconds;
            var remainingBytes = Math.Max(0, _totalBytes - completed);
            var eta = speedBytesPerSecond <= 0.1
                ? "--:--:--"
                : FormatEta(TimeSpan.FromSeconds(remainingBytes / speedBytesPerSecond));
            var line = $"{_label}: {percent:0.0}% ({SizeFormatter.Format(completed)}/{SizeFormatter.Format(_totalBytes)}) at {SizeFormatter.Format((long)speedBytesPerSecond)}/s, ETA {eta}";

            lock (Console.Out)
            {
                Console.Write("\r" + line.PadRight(Math.Max(_printed, line.Length)));
                _printed = Math.Max(_printed, line.Length);
                if (final)
                    Console.WriteLine();
            }
        }

        private static string FormatEta(TimeSpan value)
        {
            if (value.TotalHours >= 1)
                return value.ToString(@"hh\:mm\:ss");

            return value.ToString(@"mm\:ss");
        }
    }
}
