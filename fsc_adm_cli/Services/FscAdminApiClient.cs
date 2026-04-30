using fsc_adm_cli.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fsc_adm_cli.Services
{
    public sealed class FscAdminApiClient : IAsyncDisposable, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _adminApiToken;
        private readonly string _name_api_token = "X-Api-Key";

        public FscAdminApiClient(string serviceUrl, string apiToken)
        {
            _adminApiToken = apiToken;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(EnsureTrailingSlash(serviceUrl)),
                Timeout = TimeSpan.FromMinutes(10)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public Task<IReadOnlyList<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<IReadOnlyList<TenantDto>>("api/admin/tenants", cancellationToken);
        }

        public Task<TenantDto> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<TenantDto>($"api/admin/tenants/{tenantId}", cancellationToken);
        }

        public async Task<TenantDto?> GetMyTenantAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/admin/tenant/me", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            return await ReadRequiredJsonAsync<TenantDto>(response, "Failed to get current tenant", cancellationToken).ConfigureAwait(false);
        }

        public async Task<TenantDto> CreateTenantAsync(string name, CancellationToken cancellationToken = default)
        {
            using var request = CreateJsonRequest(HttpMethod.Post, "api/admin/tenants", new { name }, _adminApiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<TenantDto>(response, "Failed to create tenant", cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return GetJsonAsync<IReadOnlyList<ApiTokenDto>>($"api/admin/tenants/{tenantId}/tokens", cancellationToken);
        }

        public async Task<CreatedApiTokenResult> CreateApiTokenAsync(CreateApiTokenRequest request, CancellationToken cancellationToken = default)
        {
            using var httpRequest = CreateJsonRequest(HttpMethod.Post, "api/admin/tokens", request, _adminApiToken);
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<CreatedApiTokenResult>(response, "Failed to create API token", cancellationToken).ConfigureAwait(false);
        }

        public async Task RevokeApiTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, $"api/admin/tokens/{tokenId}/revoke", _adminApiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateApiException("Failed to revoke API token", response.StatusCode, body);
        }

        public async Task<SaveFileResultDto> UploadBytesAsync(
            string apiToken,
            string fileName,
            byte[] bytes,
            string? category,
            string? externalKey,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/file/upload");
            request.Headers.Add(_name_api_token, apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);

            if (!string.IsNullOrWhiteSpace(category))
                form.Add(new StringContent(category), "category");

            if (!string.IsNullOrWhiteSpace(externalKey))
                form.Add(new StringContent(externalKey), "externalKey");

            request.Content = form;

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<SaveFileResultDto>(response, "Failed to upload file", cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<StoredTenantFileDto>> GetFilesAsync(string apiToken, CancellationToken cancellationToken = default)
        {
            return GetJsonWithTokenAsync<IReadOnlyList<StoredTenantFileDto>>(apiToken, "api/file", cancellationToken);
        }

        public Task<StoredTenantFileDto?> TryGetFileInfoAsync(string apiToken, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            return GetOptionalJsonWithTokenAsync<StoredTenantFileDto>(apiToken, $"api/file/{fileGuid}", cancellationToken);
        }

        public Task<StoredTenantFileDto?> GetFileInfoAsync(string apiToken, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            return GetOptionalJsonWithTokenAsync<StoredTenantFileDto>(apiToken, $"api/file/{fileGuid}", cancellationToken);
        }

        public async Task<byte[]> DownloadFileAsync(string apiToken, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/file/{fileGuid}/download");
            request.Headers.Add(_name_api_token, apiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw CreateApiException("Failed to download file", response.StatusCode, body);
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteFileAsync(string apiToken, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/file/{fileGuid}");
            request.Headers.Add(_name_api_token, apiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateApiException("Failed to delete file", response.StatusCode, body);
        }

        public async Task<FileStorageBackgroundTaskDto> QueueFileStorageConsistencyCheckAsync(CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "api/admin/storage/check-consistency", _adminApiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<FileStorageBackgroundTaskDto>(
                response,
                "Failed to queue file storage consistency check",
                cancellationToken).ConfigureAwait(false);
        }

        private Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
        {
            return GetJsonWithTokenCoreAsync<T>(_adminApiToken, path, cancellationToken);
        }

        private Task<T> GetJsonWithTokenAsync<T>(string apiToken, string path, CancellationToken cancellationToken)
        {
            return GetJsonWithTokenCoreAsync<T>(apiToken, path, cancellationToken);
        }

        private async Task<T?> GetOptionalJsonWithTokenAsync<T>(string apiToken, string path, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(_name_api_token, apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return default;

            return await ReadRequiredJsonAsync<T>(response, $"Failed to GET {path}", cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> GetJsonWithTokenCoreAsync<T>(string apiToken, string path, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(_name_api_token, apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<T>(response, $"Failed to GET {path}", cancellationToken).ConfigureAwait(false);
        }

        private static async Task<T> ReadRequiredJsonAsync<T>(
            HttpResponseMessage response,
            string message,
            CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw CreateApiException(message, response.StatusCode, body);

            var result = JsonSerializer.Deserialize<T>(body, JsonOptions.Default);
            if (result is null)
                throw new InvalidOperationException($"{message}. Empty or invalid JSON response.");

            return result;
        }

        private static Exception CreateApiException(string message, HttpStatusCode statusCode, string body)
        {
            return new InvalidOperationException($"{message}. HTTP {(int)statusCode} {statusCode}. Response: {body}");
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiToken)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add(_name_api_token, apiToken);
            return request;
        }

        private HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string path, T payload, string apiToken)
        {
            var request = CreateRequest(method, path, apiToken);
            request.Content = JsonContent.Create(payload, options: JsonOptions.Default);
            return request;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public sealed record TenantDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("tenantGuid")] Guid TenantGuid,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("isActive")] bool IsActive);

    public sealed record ApiTokenDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("tokenPrefix")] string TokenPrefix,
        [property: JsonPropertyName("isActive")] bool IsActive,
        [property: JsonPropertyName("canRead")] bool CanRead,
        [property: JsonPropertyName("canWrite")] bool CanWrite,
        [property: JsonPropertyName("canDelete")] bool CanDelete,
        [property: JsonPropertyName("isAdmin")] bool IsAdmin,
        [property: JsonPropertyName("expiresUtc")] DateTimeOffset? ExpiresUtc);

    public sealed record CreateApiTokenRequest
    {
        [JsonPropertyName("tenantId")]
        public Guid TenantId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("canRead")]
        public bool CanRead { get; set; } = true;

        [JsonPropertyName("canWrite")]
        public bool CanWrite { get; set; }

        [JsonPropertyName("canDelete")]
        public bool CanDelete { get; set; }

        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }

        [JsonPropertyName("expiresUtc")]
        public DateTimeOffset? ExpiresUtc { get; set; }
    }

    public sealed record CreatedApiTokenResult(
        [property: JsonPropertyName("token")] ApiTokenDto Token,
        [property: JsonPropertyName("plainTextToken")] string PlainTextToken);

    public sealed record StoredTenantFileDto(
        [property: JsonPropertyName("tenantFileId")] Guid TenantFileId,
        [property: JsonPropertyName("fileGuid")] Guid FileGuid,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("storedFileId")] Guid StoredFileId,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("filestoreStateCompress")] FilestoreStateCompress FilestoreStateCompress,
        [property: JsonPropertyName("fileSize")] long FileSize,
        [property: JsonPropertyName("sha256")] string Sha256);

    public enum FilestoreStateCompress
    {
        NoCompressionNeeded = 0,
        CanBeCompressed = 1,
        Compressed = 2
    }

    public enum FileStorageBackgroundTaskType
    {
        MergeMultipartUpload = 0,
        CheckDatabaseConsistency = 1
    }

    public sealed record FileStorageBackgroundTaskDto(
        [property: JsonPropertyName("taskId")] Guid TaskId,
        [property: JsonPropertyName("type")] FileStorageBackgroundTaskType Type,
        [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);

    public sealed record SaveFileResultDto(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
        [property: JsonPropertyName("file")] StoredTenantFileDto? File);
}
