using Ninelives_Offline.Configuration;
using System.Data.SQLite;


namespace Ninelives_Offline.Services
{
    public class AuthenticationService
    {
        private readonly CryptographyService _cryptographyService;

        public AuthenticationService(CryptographyService cryptographyService)
        {
            _cryptographyService = cryptographyService;
        }

        public string GenerateSessionKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        public string GenerateVerificationCode()
        {
            Random random = new Random();
            int code = random.Next(100000, 999999);
            return code.ToString();
        }

        public int? GetUserIdFromSession(string sessionKey)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT userId FROM sessions WHERE sessionKey = @SessionKey";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SessionKey", sessionKey);

            object result = cmd.ExecuteScalar();
            if (result != null && int.TryParse(result.ToString(), out int userId))
            {
                return userId; // Return the userId if found
            }

            return null; // Return null if no match is found
        }

    }
}
