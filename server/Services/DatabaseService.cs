using Newtonsoft.Json;
using Ninelives_Offline.Configuration;
using Ninelives_Offline.Models;
using System.Data.SQLite;

namespace Ninelives_Offline.Services
{
    public class DatabaseService
    {
        public void EnsureDatabase()
        {
            if (!File.Exists(AppConfig.DbFile))
            {
                Console.WriteLine("Database not found. Creating new database...");
                CreateDatabase();
            }
            else
            {
                Console.WriteLine("Database loaded.");
                ClearSessionsTable();
            }
        }


        public string GenerateCsKey()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");
        }


        private void CreateDatabase()
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string createTableSql = @"
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT NOT NULL UNIQUE,
                password TEXT NOT NULL,
                sysLang TEXT NOT NULL,
                osLang TEXT NOT NULL,
                characterLimit INTEGER DEFAULT 2,
                sharedBankCount INTEGER DEFAULT 0,
                scarabGem INTEGER DEFAULT 0,
                verificationCode TEXT,
                verified BOOLEAN DEFAULT FALSE
            );

            CREATE TABLE IF NOT EXISTS sessions (
                sessionKey TEXT PRIMARY KEY,
                userId INTEGER,
                FOREIGN KEY(userId) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS characters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                CsKey TEXT,
                userId INTEGER,
                name TEXT NOT NULL,
                race INTEGER,
                job INTEGER,
                hair INTEGER,
                hairColor INTEGER,
                facialFileHead TEXT,
                facial INTEGER,
                lastZoneID INTEGER,
                portrait TEXT,  -- Added for portrait
                hireUseDefaultColor BOOLEAN DEFAULT 0,  -- Added for hireUseDefaultColor
                exp INTEGER DEFAULT 0, 
                gold INTEGER DEFAULT 0, 
                killCount INTEGER DEFAULT 0, 
                deadCount INTEGER DEFAULT 0, 
                playTime INTEGER DEFAULT 0, 
                bagSlotCount INTEGER DEFAULT 24, 
                bankPageCount INTEGER DEFAULT 1, 
                flagCode1 TEXT DEFAULT '',
                flagCode2 TEXT DEFAULT '',
                flagCode3 TEXT DEFAULT '',
                flagCode4 TEXT DEFAULT '',
                flagCode5 TEXT DEFAULT '',
                flagCode6 TEXT DEFAULT '',
                flagCode7 TEXT DEFAULT '',
                flagCode8 TEXT DEFAULT '',
                skillTreePayExp INTEGER DEFAULT 0, 
                bitCount INTEGER DEFAULT 0, 
                bits1 INTEGER DEFAULT 0, 
                bits2 INTEGER DEFAULT 0, 
                bits3 INTEGER DEFAULT 0, 
                bits4 INTEGER DEFAULT 0, 
                sessionKey TEXT,
                FOREIGN KEY(userId) REFERENCES users(id)
            );



            CREATE TABLE character_inventory (
                userId INTEGER,
                characterId INTEGER,
                dockType TEXT,
                slots TEXT, -- JSON string containing slot data
                lfc INTEGER, -- Last successful update (or something related)
                PRIMARY KEY (userId, characterId, dockType)
            );
            CREATE TABLE character_quests (
                userId INTEGER,
                characterId INTEGER,
                quests TEXT, -- JSON string containing slot data
                PRIMARY KEY (userId, characterId)
            );
            CREATE TABLE character_craftedItems (
                userId INTEGER,
                characterId INTEGER,
                craftedItems TEXT, -- JSON string containing slot data
                PRIMARY KEY (userId, characterId)
            );";

            using var cmd = new SQLiteCommand(createTableSql, conn);
            cmd.ExecuteNonQuery();
        }



        public void ClearSessionsTable()
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "DELETE FROM sessions";

            using var cmd = new SQLiteCommand(sql, conn);

            try
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine("Sessions table cleared successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error clearing sessions table: " + ex.Message);
            }
        }

        public void InitializeCharacterQuests(int userId, int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
            INSERT INTO character_quests (
                userId, characterId, quests
            ) VALUES (
                @UserId, @CharacterId, @Quests
            )";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@Quests", "[]"); // Initialize with an empty array

            cmd.ExecuteNonQuery();
        }
        public void InitializeCharacterCraftedItems(int userId, int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
            INSERT INTO character_craftedItems (
                userId, characterId, craftedItems
            ) VALUES (
                @UserId, @CharacterId, @CraftedItems
            )";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@CraftedItems", "[]"); // Initialize with an empty array

            cmd.ExecuteNonQuery();
        }
        public int GetCharacterIdByName(int userId, string characterName)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT id FROM characters WHERE userId = @UserId AND name = @Name";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Name", characterName);

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        public void RemoveCharacterInventory(int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "DELETE FROM character_inventory WHERE characterId = @CharacterId";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            cmd.ExecuteNonQuery();
        }
        public void RemoveCharacterQuests(int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "DELETE FROM character_quests WHERE characterId = @CharacterId";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            cmd.ExecuteNonQuery();
        }

        public void RemoveCharacterCraftedItems(int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();
            string sql = "DELETE FROM character_craftedItems WHERE characterId = @CharacterId";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            cmd.ExecuteNonQuery();
        }

        public void RemoveCharacter(int userId, int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "DELETE FROM characters WHERE id = @CharacterId AND userId = @UserId";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            cmd.ExecuteNonQuery();
        }

        public string GetCharacterQuests(int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT quests FROM character_quests WHERE characterId = @CharacterId";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader["quests"].ToString();
            }

            return null; // No quests found
        }

        // Update the quests for a character
        public void UpdateCharacterQuests(int characterId, string updatedQuests)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
            UPDATE character_quests
            SET quests = @Quests
            WHERE characterId = @CharacterId";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Quests", updatedQuests);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            cmd.ExecuteNonQuery();
        }

        // Add a new quest to a character's quest list
        public void AddQuestToCharacter(int characterId, int questId)
        {
            var currentQuestsJson = GetCharacterQuests(characterId);

            if (currentQuestsJson == null)
            {
                throw new Exception("Character quests not initialized.");
            }

            var quests = JsonConvert.DeserializeObject<List<dynamic>>(currentQuestsJson);
            quests.Add(new { questID = questId });

            var updatedQuestsJson = JsonConvert.SerializeObject(quests);
            UpdateCharacterQuests(characterId, updatedQuestsJson);
        }

        // Remove a quest from a character's quest list
        public void RemoveQuestFromCharacter(int characterId, int questId)
        {
            var currentQuestsJson = GetCharacterQuests(characterId);

            if (currentQuestsJson == null)
            {
                throw new Exception("Character quests not initialized.");
            }

            var quests = JsonConvert.DeserializeObject<List<dynamic>>(currentQuestsJson);
            quests.RemoveAll(q => q.questID == questId);

            var updatedQuestsJson = JsonConvert.SerializeObject(quests);
            UpdateCharacterQuests(characterId, updatedQuestsJson);
        }
        public string GetCharacterCraftedItems(int characterId)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT craftedItems FROM character_craftedItems WHERE characterId = @CharacterId";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader["craftedItems"].ToString();
            }

            return null; // No quests found
        }
        public void UpdateCharacterCraftedItems(int characterId, string craftedItems)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
            UPDATE character_craftedItems
            SET craftedItems = @CraftedItems
            WHERE characterId = @CharacterId";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CraftedItems", craftedItems);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);

            cmd.ExecuteNonQuery();
        }

        public void AddCraftedItemToCharacter(int characterId, string craftedItem)
        {
            var currentCraftedItemsJson = GetCharacterCraftedItems(characterId);

            if (currentCraftedItemsJson == null)
            {
                throw new Exception("Character Crafted Items not initialized.");
            }

            var newcraftedItems = JsonConvert.DeserializeObject<List<dynamic>>(currentCraftedItemsJson);

            // Check if the recipeID already exists
            var existingItem = newcraftedItems.FirstOrDefault(item => item.recipeID == craftedItem);

            if (existingItem != null)
            {
                existingItem.mixCount += 1;
            }
            else
            {
                // If recipeID does not exist, add it to the list
                newcraftedItems.Add(new { recipeID = craftedItem, mixCount = 1 });
            }
            var updatedCraftedItemsJson = JsonConvert.SerializeObject(newcraftedItems);
            UpdateCharacterCraftedItems(characterId, updatedCraftedItemsJson);
        }


        public Character GetCharacterByCsKey(string csKey)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string query = "SELECT * FROM characters WHERE CsKey = @CsKey LIMIT 1";
                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CsKey", csKey);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Character
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    UserId = Convert.ToInt32(reader["userId"]),
                                    Name = reader["name"].ToString(),
                                    Race = Convert.ToInt32(reader["race"]),
                                    Job = Convert.ToInt32(reader["job"]),
                                    Hair = Convert.ToInt32(reader["hair"]),
                                    HairColor = Convert.ToInt32(reader["hairColor"]),
                                    FacialFileHead = reader["facialFileHead"].ToString(),
                                    Facial = Convert.ToInt32(reader["facial"]),
                                    LastZoneID = Convert.ToInt32(reader["lastZoneID"]),
                                    Portrait = reader["portrait"].ToString(),
                                    HireUseDefaultColor = Convert.ToBoolean(reader["hireUseDefaultColor"]),
                                    CsKey = reader["CsKey"].ToString(),
                                    Exp = reader.IsDBNull(reader.GetOrdinal("exp")) ? 0 : Convert.ToInt32(reader["exp"]),
                                    Gold = reader.IsDBNull(reader.GetOrdinal("gold")) ? 0 : Convert.ToInt32(reader["gold"]),
                                    KillCount = reader.IsDBNull(reader.GetOrdinal("killCount")) ? 0 : Convert.ToInt32(reader["killCount"]),
                                    DeadCount = reader.IsDBNull(reader.GetOrdinal("deadCount")) ? 0 : Convert.ToInt32(reader["deadCount"]),
                                    PlayTime = reader.IsDBNull(reader.GetOrdinal("playTime")) ? 0 : Convert.ToInt32(reader["playTime"]),
                                    BagSlotCount = reader.IsDBNull(reader.GetOrdinal("bagSlotCount")) ? 24 : Convert.ToInt32(reader["bagSlotCount"]),
                                    BankPageCount = reader.IsDBNull(reader.GetOrdinal("bankPageCount")) ? 1 : Convert.ToInt32(reader["bankPageCount"]),
                                    FlagCode1 = reader["flagCode1"].ToString(),
                                    FlagCode2 = reader["flagCode2"].ToString(),
                                    FlagCode3 = reader["flagCode3"].ToString(),
                                    FlagCode4 = reader["flagCode4"].ToString(),
                                    FlagCode5 = reader["flagCode5"].ToString(),
                                    FlagCode6 = reader["flagCode6"].ToString(),
                                    FlagCode7 = reader["flagCode7"].ToString(),
                                    FlagCode8 = reader["flagCode8"].ToString(),
                                    SkillTreePayExp = reader.IsDBNull(reader.GetOrdinal("skillTreePayExp")) ? 0 : Convert.ToInt32(reader["skillTreePayExp"]),
                                    BitCount = reader.IsDBNull(reader.GetOrdinal("bitCount")) ? 0 : Convert.ToInt32(reader["bitCount"]),
                                    Bits1 = reader.IsDBNull(reader.GetOrdinal("bits1")) ? 0 : Convert.ToInt32(reader["bits1"]),
                                    Bits2 = reader.IsDBNull(reader.GetOrdinal("bits2")) ? 0 : Convert.ToInt32(reader["bits2"]),
                                    Bits3 = reader.IsDBNull(reader.GetOrdinal("bits3")) ? 0 : Convert.ToInt32(reader["bits3"]),
                                    Bits4 = reader.IsDBNull(reader.GetOrdinal("bits4")) ? 0 : Convert.ToInt32(reader["bits4"]),
                                    SessionKey = reader["sessionKey"].ToString()
                                };
                            }
                        }
                    }
                }

                return null; // Return null if no matching character is found
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving character by CsKey: " + ex.Message);
                return null;
            }
        }
        public Character GetCharacterByUserIdAndName(int userId, string name)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {

                    // Open the connection and log if it succeeds
                    conn.Open();
                    // Prepare the query and log it
                    string query = "SELECT * FROM characters WHERE userId = @userId AND name = @name";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        // Log parameters being added
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@name", name);

                        using (var reader = cmd.ExecuteReader())
                        {
                            // Log how many rows are returned
                            if (reader.Read())
                            {
                                // Log the character data before returning
                                var character = new Character
                                {
                                    Name = reader.GetString(reader.GetOrdinal("name")),
                                    Race = reader.GetInt32(reader.GetOrdinal("race")),
                                    Job = reader.GetInt32(reader.GetOrdinal("job")),
                                    Hair = reader.GetInt32(reader.GetOrdinal("hair")),
                                    HairColor = reader.GetInt32(reader.GetOrdinal("hairColor")),
                                    FacialFileHead = reader.GetString(reader.GetOrdinal("facialFileHead")),
                                    Facial = reader.GetInt32(reader.GetOrdinal("facial")),
                                    SessionKey = reader.GetString(reader.GetOrdinal("CsKey")),
                                    LastZoneID = reader.GetInt32(reader.GetOrdinal("lastZoneID")),
                                    BagSlotCount = reader.GetInt32(reader.GetOrdinal("bagSlotCount")),
                                    BankPageCount = reader.GetInt32(reader.GetOrdinal("bankPageCount")),
                                    Exp = reader.GetInt32(reader.GetOrdinal("exp")),
                                    Gold = reader.GetInt32(reader.GetOrdinal("gold")),
                                    KillCount = reader.GetInt32(reader.GetOrdinal("killCount")),
                                    DeadCount = reader.GetInt32(reader.GetOrdinal("deadCount")),
                                    PlayTime = reader.GetInt32(reader.GetOrdinal("playTime")),
                                    FlagCode1 = reader.GetString(reader.GetOrdinal("flagCode1")),
                                    FlagCode2 = reader.GetString(reader.GetOrdinal("flagCode2")),
                                    FlagCode3 = reader.GetString(reader.GetOrdinal("flagCode3")),
                                    FlagCode4 = reader.GetString(reader.GetOrdinal("flagCode4")),
                                    FlagCode5 = reader.GetString(reader.GetOrdinal("flagCode5")),
                                    FlagCode6 = reader.GetString(reader.GetOrdinal("flagCode6")),
                                    FlagCode7 = reader.GetString(reader.GetOrdinal("flagCode7")),
                                    FlagCode8 = reader.GetString(reader.GetOrdinal("flagCode8")),
                                    SkillTreePayExp = reader.GetInt32(reader.GetOrdinal("skilltreePayExp")),
                                    BitCount = reader.GetInt32(reader.GetOrdinal("bitCount")),
                                    Bits1 = reader.GetInt32(reader.GetOrdinal("bits1")),
                                    Bits2 = reader.GetInt32(reader.GetOrdinal("bits2")),
                                    Bits3 = reader.GetInt32(reader.GetOrdinal("bits3")),
                                    Bits4 = reader.GetInt32(reader.GetOrdinal("bits4"))
                                };

                                return character;
                            }
                            else
                            {
                                // If no character is found, log it
                                Console.WriteLine("No character found with the given UserId and Name.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur
                Console.WriteLine("Error while retrieving character: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            // If no character was found or there was an error, return null
            return null;
        }

        public bool SaveCharacter(Character character)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Check if the character already exists in the database
                    string checkCharacterSql = "SELECT COUNT(*) FROM characters WHERE CsKey = @CsKey";
                    bool characterExists = false;

                    using (var checkCmd = new SQLiteCommand(checkCharacterSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@CsKey", character.CsKey);
                        characterExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                    }

                    if (characterExists)
                    {
                        // Generate the dynamic SQL for updating the character
                        var updateFields = new List<string>();
                        var parameters = new Dictionary<string, object>();

                        // Add fields dynamically only if they are not null or empty
                        AddParameterIfNotNull(updateFields, parameters, "@UserId", "userId", character.UserId);
                        AddParameterIfNotNull(updateFields, parameters, "@Name", "name", character.Name);
                        AddParameterIfNotNull(updateFields, parameters, "@Race", "race", character.Race);
                        AddParameterIfNotNull(updateFields, parameters, "@Job", "job", character.Job);
                        AddParameterIfNotNull(updateFields, parameters, "@Hair", "hair", character.Hair);
                        AddParameterIfNotNull(updateFields, parameters, "@HairColor", "hairColor", character.HairColor);
                        AddParameterIfNotNull(updateFields, parameters, "@FacialFileHead", "facialFileHead", character.FacialFileHead);
                        AddParameterIfNotNull(updateFields, parameters, "@Facial", "facial", character.Facial);
                        AddParameterIfNotNull(updateFields, parameters, "@LastZoneID", "lastZoneID", character.LastZoneID);
                        AddParameterIfNotNull(updateFields, parameters, "@Portrait", "portrait", character.Portrait);
                        AddParameterIfNotNull(updateFields, parameters, "@HireUseDefaultColor", "hireUseDefaultColor", character.HireUseDefaultColor);
                        AddParameterIfNotNull(updateFields, parameters, "@Exp", "exp", character.Exp);
                        AddParameterIfNotNull(updateFields, parameters, "@Gold", "gold", character.Gold);
                        AddParameterIfNotNull(updateFields, parameters, "@KillCount", "killCount", character.KillCount);
                        AddParameterIfNotNull(updateFields, parameters, "@DeadCount", "deadCount", character.DeadCount);
                        AddParameterIfNotNull(updateFields, parameters, "@PlayTime", "playTime", character.PlayTime);
                        AddParameterIfNotNull(updateFields, parameters, "@BagSlotCount", "bagSlotCount", character.BagSlotCount);
                        AddParameterIfNotNull(updateFields, parameters, "@BankPageCount", "bankPageCount", character.BankPageCount);
                        AddParameterIfNotNull(updateFields, parameters, "@SkillTreePayExp", "skillTreePayExp", character.SkillTreePayExp);
                        AddParameterIfNotNull(updateFields, parameters, "@BitCount", "bitCount", character.BitCount);
                        AddParameterIfNotNull(updateFields, parameters, "@Bits1", "bits1", character.Bits1);
                        AddParameterIfNotNull(updateFields, parameters, "@Bits2", "bits2", character.Bits2);
                        AddParameterIfNotNull(updateFields, parameters, "@Bits3", "bits3", character.Bits3);
                        AddParameterIfNotNull(updateFields, parameters, "@Bits4", "bits4", character.Bits4);
                        AddParameterIfNotNull(updateFields, parameters, "@SessionKey", "sessionKey", character.SessionKey);
                        AddParameterIfNotNull(updateFields, parameters, "@CsKey", "CsKey", character.CsKey);

                        //flags
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode1", "FlagCode1", character.FlagCode1);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode2", "FlagCode2", character.FlagCode2);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode3", "FlagCode3", character.FlagCode3);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode4", "FlagCode4", character.FlagCode4);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode5", "FlagCode5", character.FlagCode5);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode6", "FlagCode6", character.FlagCode6);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode7", "FlagCode7", character.FlagCode7);
                        AddParameterIfNotNull(updateFields, parameters, "@FlagCode8", "FlagCode8", character.FlagCode8);


                        if (updateFields.Count > 0)
                        {
                            string updateSql = $"UPDATE characters SET {string.Join(", ", updateFields)} WHERE CsKey = @CsKey";

                            using (var updateCmd = new SQLiteCommand(updateSql, conn))
                            {
                                foreach (var param in parameters)
                                {
                                    updateCmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                    }

                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving character: " + ex.Message);
                return false;
            }
        }

        private void AddParameterIfNotNull(List<string> fields, Dictionary<string, object> parameters, string paramName, string columnName, object value)
        {
            if (value != null && !(value is string str && string.IsNullOrEmpty(str)))
            {
                fields.Add($"{columnName} = {paramName}");
                parameters.Add(paramName, value ?? DBNull.Value);
            }
        }



        private void BindCharacterParameters(SQLiteCommand cmd, Character character)
        {
            cmd.Parameters.AddWithValue("@UserId", character.UserId);
            cmd.Parameters.AddWithValue("@Name", character.Name);
            cmd.Parameters.AddWithValue("@Race", character.Race);
            cmd.Parameters.AddWithValue("@Job", character.Job);
            cmd.Parameters.AddWithValue("@Hair", character.Hair);
            cmd.Parameters.AddWithValue("@HairColor", character.HairColor);
            cmd.Parameters.AddWithValue("@FacialFileHead", character.FacialFileHead);
            cmd.Parameters.AddWithValue("@Facial", character.Facial);
            cmd.Parameters.AddWithValue("@LastZoneID", character.LastZoneID);
            cmd.Parameters.AddWithValue("@Portrait", character.Portrait);
            cmd.Parameters.AddWithValue("@HireUseDefaultColor", character.HireUseDefaultColor);
            cmd.Parameters.AddWithValue("@Exp", character.Exp);
            cmd.Parameters.AddWithValue("@Gold", character.Gold);
            cmd.Parameters.AddWithValue("@KillCount", character.KillCount);
            cmd.Parameters.AddWithValue("@DeadCount", character.DeadCount);
            cmd.Parameters.AddWithValue("@PlayTime", character.PlayTime);
            cmd.Parameters.AddWithValue("@BagSlotCount", character.BagSlotCount);
            cmd.Parameters.AddWithValue("@BankPageCount", character.BankPageCount);
            cmd.Parameters.AddWithValue("@SkillTreePayExp", character.SkillTreePayExp);
            cmd.Parameters.AddWithValue("@BitCount", character.BitCount);
            cmd.Parameters.AddWithValue("@Bits1", character.Bits1);
            cmd.Parameters.AddWithValue("@Bits2", character.Bits2);
            cmd.Parameters.AddWithValue("@Bits3", character.Bits3);
            cmd.Parameters.AddWithValue("@Bits4", character.Bits4);
            cmd.Parameters.AddWithValue("@SessionKey", character.SessionKey ?? "");
            cmd.Parameters.AddWithValue("@CsKey", character.CsKey ?? "");
        }



        // Check if inventory exists for the given characterId and dockType
        public bool CheckIfInventoryExists(int userId, int characterId, string dockType)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT COUNT(*) FROM character_inventory WHERE userId = @UserId AND characterId = @CharacterId AND dockType = @DockType";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@DockType", dockType);
            long count = (long)cmd.ExecuteScalar();
            return count > 0;
        }

        // Create a new character inventory
        public void CreateInventory(string csKey, string dockType, string slotsJson)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
    INSERT INTO character_inventory (csKey, dockType, slots, lfc)
    VALUES (@CsKey, @DockType, @Slots, @Lfc)";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CsKey", csKey);
            cmd.Parameters.AddWithValue("@DockType", dockType);
            cmd.Parameters.AddWithValue("@Slots", slotsJson);
            cmd.Parameters.AddWithValue("@Lfc", 0);

            cmd.ExecuteNonQuery();
        }


        public void UpdateInventory(int userId, int characterId, string dockType, string slotsJson, int lfc)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = @"
                UPDATE character_inventory 
                SET slots = @Slots, lfc = @Lfc 
                WHERE userId = @UserId AND characterId = @CharacterId AND dockType = @DockType";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@DockType", dockType);
            cmd.Parameters.AddWithValue("@Slots", slotsJson);
            cmd.Parameters.AddWithValue("@Lfc", lfc);

            cmd.ExecuteNonQuery();
        }

        // Get inventory for a character and dockType
        public string GetInventory(int userId, int characterId, string dockType)
        {
            using var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;");
            conn.Open();

            string sql = "SELECT slots FROM character_inventory WHERE userId = @UserId AND characterId = @CharacterId AND dockType = @DockType";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CharacterId", characterId);
            cmd.Parameters.AddWithValue("@DockType", dockType);

            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }

    }
}
