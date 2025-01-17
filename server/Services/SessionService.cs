using Ninelives_Offline.Configuration;
using System.Data.SQLite;

namespace Ninelives_Offline.Services
{
    public class SessionService
    {
        // Method to get the UserId associated with a session key
        public int GetUserIdBySession(string sessionKey)
        {
            using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
            {
                conn.Open();

                // SQL to get userId based on sessionKey
                string checkSessionSql = "SELECT userId FROM sessions WHERE sessionKey = @SessionKey";
                using (var cmd = new SQLiteCommand(checkSessionSql, conn))
                {
                    cmd.Parameters.AddWithValue("@SessionKey", sessionKey);
                    object result = cmd.ExecuteScalar();

                    // Return the userId if found
                    if (result != null)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }

            return -1; // Return -1 if session key is not valid
        }
    }
}
