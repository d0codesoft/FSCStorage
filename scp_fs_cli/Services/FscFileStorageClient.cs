using scp_fs_cli.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace scp_fs_cli.Services
{
    public sealed class FscFileStorageClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        private static class ApiKeyAuthenticationOptions
        {
            public static string ApiKey { get; } = "X-Api-Key";
            public static string TokenId { get; } = "Token-Id";
        }

        public FscFileStorageClient(ClientConfig config)
        {
            Config = config;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(EnsureTrailingSlash(config.ServiceUrl)),
                Timeout = Timeout.InfiniteTimeSpan
            };

            _httpClient.DefaultRequestHeaders.Add(ApiKeyAuthenticationOptions.ApiKey, config.ApiToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public ClientConfig Config { get; }
        public string ServiceUrl => Config.ServiceUrl;

        public async Task<MultipartUploadStatusResult> GetMultipartStatusAsync(Guid uploadId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/multipart/{uploadId}/status", cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<MultipartUploadStatusResult>(
                response,
                "Multipart status failed",
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<SaveFileResult> UploadWholeFileAsync(
            FileInfo fileInfo,
            string? category,
            string? externalKey,
            Action<long> onBytesRead,
            CancellationToken cancellationToken = default)
        {
            await using var stream = new ProgressReadStream(fileInfo.OpenRead(), onBytesRead);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(ContentTypes.Get(fileInfo.Extension));

            content.Add(fileContent, "file", fileInfo.Name);

            if (!string.IsNullOrWhiteSpace(category))
                content.Add(new StringContent(category), "category");

            if (!string.IsNullOrWhiteSpace(externalKey))
                content.Add(new StringContent(externalKey), "externalKey");

            using var response = await _httpClient.PostAsync("api/files", content, cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<SaveFileResult>(
                response,
                "Simple upload failed",
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<InitMultipartUploadResult> InitMultipartAsync(
            InitMultipartUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/multipart/init",
                request,
                JsonOptions.Default,
                cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<InitMultipartUploadResult>(
                response,
                "Multipart init failed",
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<UploadMultipartPartResult> UploadPartAsync(
            FileInfo fileInfo,
            Guid uploadId,
            int partNumber,
            long partSize,
            Action<long> onBytesRead,
            CancellationToken cancellationToken = default)
        {
            var offset = (partNumber - 1L) * partSize;
            var length = Math.Min(partSize, fileInfo.Length - offset);
            if (length <= 0)
                throw new InvalidOperationException($"Invalid part length for part {partNumber}.");

            await using var fileStream = new FileStream(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 128,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            fileStream.Position = offset;

            await using var limitedStream = new LimitedReadStream(fileStream, length);
            await using var trackedStream = new ProgressReadStream(limitedStream, onBytesRead);
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(trackedStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", fileInfo.Name);

            using var response = await _httpClient.PutAsync(
                $"api/multipart/{uploadId}/parts/{partNumber}",
                content,
                cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<UploadMultipartPartResult>(
                response,
                $"Multipart part {partNumber} failed",
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<AbortMultipartUploadResult> AbortMultipartAsync(Guid uploadId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/multipart/{uploadId}/abort", content: null, cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<AbortMultipartUploadResult>(
                response,
                "Multipart abort failed",
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<CompleteMultipartUploadResult> CompleteMultipartAsync(Guid uploadId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync(
                $"api/multipart/{uploadId}/complete",
                content: null,
                cancellationToken).ConfigureAwait(false);

            return await ReadRequiredJsonAsync<CompleteMultipartUploadResult>(
                response,
                "Multipart complete failed",
                cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public static Exception CreateApiException(string message, HttpStatusCode statusCode, string responseText)
        {
            var body = string.IsNullOrWhiteSpace(responseText) ? "<empty>" : responseText;
            return new InvalidOperationException($"{message}. HTTP {(int)statusCode} {statusCode}. Response: {body}");
        }

        private static async Task<T> ReadRequiredJsonAsync<T>(
            HttpResponseMessage response,
            string message,
            CancellationToken cancellationToken)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw CreateApiException(message, response.StatusCode, responseText);

            return JsonSerializer.Deserialize<T>(responseText, JsonOptions.Default)
                ?? throw new InvalidOperationException($"{message}. Server returned invalid response.");
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }
    }
}
