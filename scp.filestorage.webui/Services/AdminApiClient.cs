using System.Net;
using System.Net.Http.Json;
using scp.filestorage.webui.Models;

namespace scp.filestorage.webui.Services
{
    public sealed class AdminApiClient
    {
        private readonly HttpClient _httpClient;

        public AdminApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<StorageStatisticsViewModel> GetStorageStatisticsAsync(
            int largestFilesLimit = 25,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(
                $"api/admin/storage/statistics?largestFilesLimit={largestFilesLimit}",
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<StorageStatisticsViewModel>(
                cancellationToken: cancellationToken)
                ?? new StorageStatisticsViewModel();
        }

        public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetActiveBackgroundTasksAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetBackgroundTasksAsync("api/admin/storage/tasks/active", cancellationToken);
        }

        public async Task<IReadOnlyList<BackgroundTaskViewModel>> GetCompletedBackgroundTasksAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return await GetBackgroundTasksAsync(
                $"api/admin/storage/tasks/completed?limit={limit}",
                cancellationToken);
        }

        public async Task<BackgroundTaskViewModel?> GetBackgroundTaskAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"api/admin/storage/tasks/{taskId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<BackgroundTaskViewModel>(
                cancellationToken: cancellationToken);
        }

        public async Task QueueConsistencyCheckAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsync("api/admin/storage/check-consistency", null, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }

        private async Task<IReadOnlyList<BackgroundTaskViewModel>> GetBackgroundTasksAsync(
            string url,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<BackgroundTaskViewModel>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "API token is missing or invalid.",
                HttpStatusCode.Forbidden => "This page requires an admin API token.",
                _ => string.IsNullOrWhiteSpace(body)
                    ? $"Request failed with status {(int)response.StatusCode}."
                    : body
            };

            throw new InvalidOperationException(message);
        }
    }
}
