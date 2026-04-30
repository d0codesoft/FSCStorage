using System.Text.Json;
using System.Text.Json.Serialization;

namespace fsc_adm_cli.Infrastructure
{
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static readonly JsonSerializerOptions Pretty = new(Default)
        {
            WriteIndented = true
        };
    }
}
