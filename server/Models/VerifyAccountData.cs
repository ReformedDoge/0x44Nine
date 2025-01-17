using Newtonsoft.Json;

namespace Ninelives_Offline.Models
{
    public class VerifyAccountData
    {
        [JsonProperty("ver_major")]
        public string VerMajor { get; set; }

        [JsonProperty("ver_minor")]
        public string VerMinor { get; set; }

        [JsonProperty("ver_rivision")]
        public string VerRivision { get; set; }

        [JsonProperty("id")]
        public string Email { get; set; }

        [JsonProperty("verifyCode")]
        public string VerificationCode { get; set; }
    }
}
