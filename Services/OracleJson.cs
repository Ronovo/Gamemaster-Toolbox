using System.Text.Json;
using System.Text.Json.Serialization;

namespace GM_Toolbox.Services
{
    public static class OracleJson
    {
        /// <summary>
        /// One set of JSON settings shared by export and import.
        ///
        /// Both directions MUST use this same instance. If export writes camelCase and
        /// import expects PascalCase, every round trip silently produces empty objects
        /// instead of an error.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
