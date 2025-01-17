using Newtonsoft.Json;

namespace Ninelives_Offline.Models
{
    public class UserData
    {
        [JsonProperty("id")]
        public string Email { get; set; }

        [JsonProperty("pass")]
        public string Password { get; set; }

        [JsonProperty("sysLang")]
        public string SysLang { get; set; }

        [JsonProperty("osLang")]
        public string OsLang { get; set; }

        [JsonProperty("uiLang")]
        public string UiLang { get; set; }

        public int ScarabGem { get; set; }
        public int CharacterLimit { get; set; }
        [JsonIgnore]
        public Dictionary<string, object> ExtraFields { get; set; }
    }
}
