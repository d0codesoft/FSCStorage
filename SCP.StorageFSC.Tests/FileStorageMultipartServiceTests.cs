using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using scp.filestorage.Data.Dto;
using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using scp.filestorage.Services;
using System.Security.Cryptography;
using System.Text;

namespace SCP.StorageFSC.Tests;

public sealed class FileStorageMultipartServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "scp-multipart-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryMultipartUploadSessionRepository _sessions = new();
    private readonly InMemoryMultipartUploadPartRepository _parts = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public FileStorageMultipartServiceTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task InitAsync_CreatesSessionWithExpectedPartCount()
    {
        var sut = CreateService();

        var result = await sut.InitAsync(new InitMultipartUploadRequestDto
        {
            TenantId = _tenantId,
            FileName = "video.mp4",
            FileSize = 5,
            PartSize = 2,
            ContentType = "video/mp4"
        });

        Assert.NotEqual(Guid.Empty, result.UploadId);
        Assert.Equal(_tenantId, result.TenantId);
        Assert.Equal("video.mp4", result.FileName);
        Assert.Equal(5, result.FileSize);
        Assert.Equal(2, result.PartSize);
        Assert.Equal(3, result.TotalParts);
        Assert.Equal(MultipartUploadStatus.Created, result.Status);

        var session = Assert.Single(_sessions.Items);
        Assert.Equal(result.UploadId, session.UploadId);
        Assert.True(Directory.Exists(Path.Combine(_rootPath, session.TempStoragePrefix)));
    }

    [Fact]
    public async Task UploadPartAsync_WritesPartAndMovesSessionToUploading()
    {
        var sut = CreateService();
        var init = await InitAsync(sut);
        var bytes = Encoding.UTF8.GetBytes("he");

        var result = await sut.UploadPartAsync(new UploadMultipartPartRequestDto
        {
            UploadId = init.UploadId,
            PartNumber = 1,
            Content = new MemoryStream(bytes),
            ContentLength = bytes.Length,
            PartChecksumSha256 = Sha256(bytes)
        });

        Assert.Equal(init.UploadId, result.UploadId);
        Assert.Equal(1, result.PartNumber);
        Assert.Equal(0, result.OffsetBytes);
        Assert.Equal(bytes.Length, result.SizeInBytes);
        Assert.Equal(MultipartUploadPartStatus.Uploaded, result.Status);
        Assert.Equal(Sha256(bytes), result.ChecksumSha256);

        var part = Assert.Single(_parts.Items);
        Assert.True(File.Exists(Path.Combine(_rootPath, part.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(MultipartUploadStatus.Uploading, Assert.Single(_sessions.Items).Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenAllPartsUploaded_MergesFileAndCleansTempParts()
    {
        var sut = CreateService();
        var content = Encoding.UTF8.GetBytes("hello");
        var init = await InitAsync(sut, expectedChecksum: Sha256(content));

        await UploadPartAsync(sut, init.UploadId, 1, "he");
        await UploadPartAsync(sut, init.UploadId, 2, "ll");
        await UploadPartAsync(sut, init.UploadId, 3, "o");

        var result = await sut.CompleteAsync(new CompleteMultipartUploadRequestDto
        {
            UploadId = init.UploadId
        });

        Assert.Equal(MultipartUploadStatus.Completed, result.Status);
        Assert.Equal(Sha256(content), result.FinalChecksumSha256);
        Assert.True(File.Exists(result.PhysicalPath));
        Assert.Equal("hello", await File.ReadAllTextAsync(result.PhysicalPath));
        Assert.All(_parts.Items, part => Assert.Equal(MultipartUploadPartStatus.Verified, part.Status));

        var session = Assert.Single(_sessions.Items);
        Assert.Equal(MultipartUploadStatus.Completed, session.Status);
        Assert.False(Directory.Exists(Path.Combine(_rootPath, session.TempStoragePrefix)));
    }

    [Fact]
    public async Task AbortAsync_RemovesUploadedPartsAndMarksSessionAborted()
    {
        var sut = CreateService();
        var init = await InitAsync(sut);
        await UploadPartAsync(sut, init.UploadId, 1, "he");
        var session = Assert.Single(_sessions.Items);
        Assert.True(Directory.Exists(Path.Combine(_rootPath, session.TempStoragePrefix)));

        var result = await sut.AbortAsync(init.UploadId);

        Assert.Equal(init.UploadId, result.UploadId);
        Assert.Equal(MultipartUploadStatus.Aborted, result.Status);
        Assert.Empty(_parts.Items);
        Assert.Equal(MultipartUploadStatus.Aborted, session.Status);
        Assert.False(Directory.Exists(Path.Combine(_rootPath, session.TempStoragePrefix)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private FileStorageMultipartService CreateService()
    {
        return new FileStorageMultipartService(
            _sessions,
            _parts,
            Options.Create(new FileStorageMultipartOptions
            {
                RootPath = _rootPath,
                TempFolderName = "_multipart",
                FilesFolderName = "files",
                MinPartSizeBytes = 1,
                MaxPartSizeBytes = 10
            }),
            NullLogger<FileStorageMultipartService>.Instance);
    }

    private Task<InitMultipartUploadResultDto> InitAsync(
        FileStorageMultipartService sut,
        string? expectedChecksum = null)
    {
        return sut.InitAsync(new InitMultipartUploadRequestDto
        {
            TenantId = _tenantId,
            FileName = "hello.txt",
            FileSize = 5,
            PartSize = 2,
            ContentType = "text/plain",
            ExpectedChecksumSha256 = expectedChecksum
        });
    }

    private static Task<UploadMultipartPartResultDto> UploadPartAsync(
        FileStorageMultipartService sut,
        Guid uploadId,
        int partNumber,
        string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return sut.UploadPartAsync(new UploadMultipartPartRequestDto
        {
            UploadId = uploadId,
            PartNumber = partNumber,
            Content = new MemoryStream(bytes),
            ContentLength = bytes.Length,
            PartChecksumSha256 = Sha256(bytes)
        });
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed class InMemoryMultipartUploadSessionRepository : IMultipartUploadSessionRepository
    {
        private readonly List<MultipartUploadSession> _items = [];
        public IReadOnlyList<MultipartUploadSession> Items => _items;

        public Task<Guid> InsertAsync(MultipartUploadSession session, CancellationToken cancellationToken = default)
        {
            _items.Add(session);
            return Task.FromResult(session.Id);
        }

        public Task<MultipartUploadSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(session => session.Id == id));
        }

        public Task<MultipartUploadSession?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(session => session.PublicId == publicId));
        }

        public Task<MultipartUploadSession?> GetByUploadIdAsync(Guid uploadId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(session => session.UploadId == uploadId));
        }

        public Task<IReadOnlyList<MultipartUploadSession>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MultipartUploadSession>>(_items.Where(session => session.TenantId == tenantId).ToList());
        }

        public Task<IReadOnlyList<MultipartUploadSession>> GetExpiredPendingAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MultipartUploadSession>>(
                _items.Where(session =>
                    session.ExpiresAtUtc.HasValue &&
                    session.ExpiresAtUtc.Value <= utcNow &&
                    session.Status is MultipartUploadStatus.Created or MultipartUploadStatus.Uploading).ToList());
        }

        public Task<bool> UpdateAsync(MultipartUploadSession session, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == session.Id));
        }

        public Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadStatus status,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? failedAtUtc = null,
            DateTime? completedAtUtc = null,
            Guid? storedFileId = null,
            CancellationToken cancellationToken = default)
        {
            var session = _items.FirstOrDefault(item => item.Id == id);
            if (session is null)
                return Task.FromResult(false);

            session.Status = status;
            session.ErrorCode = errorCode;
            session.ErrorMessage = errorMessage;
            session.FailedAtUtc = failedAtUtc;
            session.CompletedAtUtc = completedAtUtc;
            session.StoredFileId = storedFileId;
            session.UpdatedUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task<bool> TouchUpdatedAsync(Guid id, DateTime updatedUtc, CancellationToken cancellationToken = default)
        {
            var session = _items.FirstOrDefault(item => item.Id == id);
            if (session is null)
                return Task.FromResult(false);

            session.UpdatedUtc = updatedUtc;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(session => session.Id == id) > 0);
        }
    }

    private sealed class InMemoryMultipartUploadPartRepository : IMultipartUploadPartRepository
    {
        private readonly List<MultipartUploadPart> _items = [];
        public IReadOnlyList<MultipartUploadPart> Items => _items;

        public Task<Guid> InsertAsync(MultipartUploadPart part, CancellationToken cancellationToken = default)
        {
            _items.Add(part);
            return Task.FromResult(part.Id);
        }

        public Task<MultipartUploadPart?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(part => part.Id == id));
        }

        public Task<MultipartUploadPart?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(part => part.PublicId == publicId));
        }

        public Task<MultipartUploadPart?> GetBySessionAndPartNumberAsync(
            Guid multipartUploadSessionId,
            int partNumber,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(part =>
                part.MultipartUploadSessionId == multipartUploadSessionId &&
                part.PartNumber == partNumber));
        }

        public Task<IReadOnlyList<MultipartUploadPart>> GetBySessionIdAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MultipartUploadPart>>(
                _items.Where(part => part.MultipartUploadSessionId == multipartUploadSessionId).ToList());
        }

        public Task<int> CountUploadedPartsAsync(Guid multipartUploadSessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Count(part =>
                part.MultipartUploadSessionId == multipartUploadSessionId &&
                part.Status is MultipartUploadPartStatus.Uploaded or MultipartUploadPartStatus.Verified));
        }

        public Task<bool> UpsertAsync(MultipartUploadPart part, CancellationToken cancellationToken = default)
        {
            var existing = _items.FindIndex(item => item.Id == part.Id);
            if (existing >= 0)
                _items[existing] = part;
            else
                _items.Add(part);

            return Task.FromResult(true);
        }

        public Task<bool> UpdateAsync(MultipartUploadPart part, CancellationToken cancellationToken = default)
        {
            var existing = _items.FindIndex(item => item.Id == part.Id);
            if (existing < 0)
                return Task.FromResult(false);

            _items[existing] = part;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadPartStatus status,
            DateTime? uploadedAtUtc = null,
            string? errorMessage = null,
            int? retryCount = null,
            DateTime? lastFailedAtUtc = null,
            string? checksumSha256 = null,
            string? providerPartETag = null,
            CancellationToken cancellationToken = default)
        {
            var part = _items.FirstOrDefault(item => item.Id == id);
            if (part is null)
                return Task.FromResult(false);

            part.Status = status;
            part.UploadedAtUtc = uploadedAtUtc;
            part.ErrorMessage = errorMessage;
            part.RetryCount = retryCount ?? part.RetryCount;
            part.LastFailedAtUtc = lastFailedAtUtc;
            part.ChecksumSha256 = checksumSha256;
            part.ProviderPartETag = providerPartETag;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteBySessionIdAsync(Guid multipartUploadSessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(part => part.MultipartUploadSessionId == multipartUploadSessionId) > 0);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(part => part.Id == id) > 0);
        }
    }
}
