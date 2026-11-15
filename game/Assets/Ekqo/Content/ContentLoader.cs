using Ekqo.Core.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ekqo.Content
{
    /// <summary>Reads a compiled bundle (build/content/&lt;edition&gt;/bundle.json) into the Core models.</summary>
    public static class ContentLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                // JSON is snake_case; dictionary keys (step ids, slot names) are kept as written.
                NamingStrategy = new SnakeCaseNamingStrategy { ProcessDictionaryKeys = false },
            },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static Bundle Parse(string json) => JsonConvert.DeserializeObject<Bundle>(json, Settings);
    }
}
