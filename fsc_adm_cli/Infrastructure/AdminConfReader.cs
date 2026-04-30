using System.Text.Json;
using System.Text.Json.Serialization;

namespace fsc_adm_cli.Infrastructure
{
    public static class AdminConfReader
    {
        public static async Task<AdminConfModel> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                throw new AdminConfException($"admin.conf file was not found: {path}");

            AdminConfModel? model;
            try
            {
                await using var stream = File.OpenRead(path);
                model = await JsonSerializer.DeserializeAsync<AdminConfModel>(
                    stream,
                    JsonOptions.Default,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new AdminConfException($"admin.conf contains invalid JSON: {path}. {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new AdminConfException($"admin.conf could not be read: {path}. {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new AdminConfException($"admin.conf could not be read because access was denied: {path}. {ex.Message}", ex);
            }

            if (model is null || string.IsNullOrWhiteSpace(model.Key))
                throw new AdminConfException($"admin.conf is invalid: {path}. Required setting 'Key' is missing or empty.");

            return model;
        }
    }

    public sealed record AdminConfModel(
        [property: JsonPropertyName("Name")] string? Name,
        [property: JsonPropertyName("Key")] string Key);
}
