using SCP.StorageFSC.Services;

namespace SCP.StorageFSC.Tests;

public sealed class FileTransferLimiterTests
{
    [Fact]
    public async Task AcquireUploadSlotAsync_WhenUploadLimitReached_ThrowsIOException()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 1,
            maxConcurrentDownloads: 1,
            uploadBytesPerSecond: 1024 * 1024,
            downloadBytesPerSecond: 1024 * 1024);
        await using var slot = await limiter.AcquireUploadSlotAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(async () =>
            await limiter.AcquireUploadSlotAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcquireDownloadSlotAsync_WhenDownloadLimitReached_ThrowsIOException()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 1,
            maxConcurrentDownloads: 1,
            uploadBytesPerSecond: 1024 * 1024,
            downloadBytesPerSecond: 1024 * 1024);
        await using var slot = await limiter.AcquireDownloadSlotAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(async () =>
            await limiter.AcquireDownloadSlotAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitSpeedAsync_WhenChunkIsLargerThanOneSecondLimit_SplitsPermits()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 1,
            maxConcurrentDownloads: 1,
            uploadBytesPerSecond: 1024 * 1024,
            downloadBytesPerSecond: 1024 * 1024);

        await limiter.WaitUploadSpeedAsync(1024 * 1024 + 1, TestContext.Current.CancellationToken);
    }
}
