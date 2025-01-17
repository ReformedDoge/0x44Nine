using Newtonsoft.Json;
using System.Reflection;


namespace save_grabber.models
{
    public class LoginResponse
    {
        public int CharacterLimit { get; set; }
        public int SharedBankCount { get; set; }
        public int ScarabGem { get; set; }
        public string SessionKey { get; set; }
    }

    public class UserData
    {
        [JsonProperty("id")]
        public string Email { get; set; }

        [JsonProperty("pass")]
        public string Password { get; set; }

        [JsonProperty("osLang")]
        public string OsLang { get; set; } = "English";

        [JsonProperty("uiLang")]
        public string UiLang { get; set; } = "UK";
        public string Ver_major { get; set; } = "0";
        public string Ver_minor { get; set; } = "24";
        public string Ver_rivision { get; set; } = "3";

        public int ScarabGem { get; set; }
        public int CharacterLimit { get; set; }
        public string SessionKey { get; set; }
        public int SharedBankCount { get; set; }
        // Add this for character list storage
        public List<Dictionary<string, object>> CharacterList { get; set; }
        public bool Verified { get; set; } = true;




        // Override ToString() dynamically
        public override string ToString()
        {
            var properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            string result = "";

            foreach (var prop in properties)
            {
                var value = prop.GetValue(this, null);
                result += $"{prop.Name}: {value}, ";
            }

            // Remove trailing comma and space
            if (result.EndsWith(", "))
            {
                result = result.Substring(0, result.Length - 2);
            }

            return result;
        }
    }
}
