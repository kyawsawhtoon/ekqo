using Ekqo.Core.Engine;
using Newtonsoft.Json;

namespace Ekqo.Content
{
    /// <summary>
    /// Turns a save into JSON and back. Until the backend exists (architecture 3, build order)
    /// the game keeps it in browser storage through PlayerPrefs.
    /// </summary>
    public static class SaveStore
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string ToJson(SaveSnapshot save) => JsonConvert.SerializeObject(save, Settings);

        /// <summary>Null when the text is empty or not a save, so the game starts fresh.</summary>
        public static SaveSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<SaveSnapshot>(json, Settings); }
            catch (System.Exception) { return null; }
        }
    }
}
