using System.Text.Json;
using System.Text.Json.Serialization;

namespace GM_Toolbox.Services
{
    public static class OracleJson
    {
        /// <summary>
        /// The JSON settings export and import share, so both use the same property casing.
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
