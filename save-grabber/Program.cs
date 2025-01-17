using Newtonsoft.Json;
using save_grabber.models;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;


// build:
// dotnet publish -c Release -r win-x64 --self-contained false
// dotnet publish -c Release -r win-x86 --self-contained false
namespace SaveGrabber
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Create a UserData object and prompt for email and password
            UserData userData = new UserData();
            Console.WriteLine("Welcome to the 0x44Nine Save tool!");
            Console.WriteLine("Test Version - Contact: 0x44oge on Discord!" + Environment.NewLine);
            Console.Write("Enter your email: ");
            userData.Email = Console.ReadLine();
            Console.Write("Enter your password: ");
            userData.Password = Console.ReadLine();
            // Set additional default values for UserData
            userData.OsLang = "English";
            userData.UiLang = "UK";

            var slotTypeToDockType = new Dictionary<string, int>
        {
            { "statsItems", 1 },
            { "bagItems", 2 },
            { "actionItems", 4 },
            { "bankItems", 7 },
            { "shareItems", 11 },
        };

            // Declare the handlers list first
            var handlers = new List<RequestHandler>();

            // Add the login handler
            handlers.Add(new RequestHandler
            {
                Endpoint = "http://path.smokymonkeys.com:8080/kyrill/login",
                PrepareRequestData = (userData) => new Dictionary<string, string>
            {
                { "ver_major", userData.Ver_major },
                { "ver_minor", userData.Ver_minor },
                { "ver_rivision", userData.Ver_rivision },
                { "id", userData.Email },
                { "pass", userData.Password },
                { "osLang", userData.OsLang },
                { "uiLang", userData.UiLang },
            },
                ProcessResponse = (response, userData) =>
                {
                    try
                    {
                        // Deserialize the JSON into the LoginResponse class
                        var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(response);

                        if (loginResponse != null)
                        {
                            // Access the properties directly
                            userData.SessionKey = loginResponse.SessionKey;
                            userData.CharacterLimit = loginResponse.CharacterLimit;
                            userData.SharedBankCount = loginResponse.SharedBankCount;
                            userData.ScarabGem = 10000; //loginResponse.ScarabGem;


                            Console.WriteLine("Login Response Processed.");
                        }
                        else
                        {
                            Console.WriteLine("Failed to process login response: Response is null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing login response: {ex.Message}");
                    }
                }
            });

            // Add the character list handler
            handlers.Add(new RequestHandler
            {
                Endpoint = "http://path.smokymonkeys.com:8080/kyrill/characterList",
                PrepareRequestData = (userData) => new Dictionary<string, string>
            {
                { "ver_major", userData.Ver_major },
                { "ver_minor", userData.Ver_minor },
                { "ver_rivision", userData.Ver_rivision },
                { "id", userData.Email },
                { "sessionKey", userData.SessionKey },
                { "osLang", userData.OsLang },
                { "uiLang", userData.UiLang },
            },
                ProcessResponse = (response, userData) =>
                {
                    var responseArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(response);
                    if (responseArray != null)
                    {
                        Console.WriteLine("Character List Response Processed.");
                        userData.CharacterList = responseArray;
                    }
                }
            });

            // Process login and character list first
            foreach (var handler in handlers)
            {
                var requestData = handler.PrepareRequestData(userData);
                var encryptedRequestData = MakeAESRequestPostData(requestData);
                var response = await SendPostRequest(handler.Endpoint, encryptedRequestData);

                if (response.Length >= 40)
                {
                    var decryptedResponse = AESDecodeResultForOnline(response);
                    handler.ProcessResponse(decryptedResponse, userData);
                }

                await Task.Delay(4000);
            }
            // Now process each character and their inventories sequentially
            foreach (var character in userData.CharacterList)
            {
                var characterName = character["name"].ToString();
                Console.WriteLine($"Processing character: {characterName}");

                // Load character
                var loadCharacterResponse = await ProcessSingleRequest(
                    "http://path.smokymonkeys.com:8080/kyrill/loadCharacter",
                    new Dictionary<string, string>
                    {
                    { "ver_major", userData.Ver_major },
                    { "ver_minor", userData.Ver_minor },
                    { "ver_rivision", userData.Ver_rivision },
                    { "id", userData.Email },
                    { "name", characterName },
                    { "sessionKey", userData.SessionKey },
                    },
                    userData
                );

                if (!string.IsNullOrEmpty(loadCharacterResponse))
                {
                    var characterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(loadCharacterResponse);
                    if (characterData != null)
                    {
                        // Update character data in the list
                        var existingCharacter = userData.CharacterList.First(c => c["name"].ToString() == characterName);
                        foreach (var kvp in characterData)
                        {
                            existingCharacter[kvp.Key] = kvp.Value;
                        }

                        // Process all inventory types for this character
                        var characterSessionKey = characterData["sessionKey"].ToString();
                        foreach (var slotType in slotTypeToDockType)
                        {
                            var inventoryResponse = await ProcessSingleRequest(
                            "http://path.smokymonkeys.com:8080/kyrill/loadCharacterItems",
                            new Dictionary<string, string>
                            {
                                { "ver_major", userData.Ver_major },
                                { "ver_minor", userData.Ver_minor },
                                { "ver_rivision", userData.Ver_rivision },
                                { "id", userData.Email },
                                { "sessionKey", userData.SessionKey },
                                { "csKey", characterSessionKey },
                                { "dockType", slotType.Value.ToString() }
                            },
                            userData
                        );
                            if (!string.IsNullOrEmpty(inventoryResponse))
                            {
                                try
                                {
                                    var inventoryData = JsonConvert.DeserializeObject<object>(inventoryResponse);
                                    // Store the inventory data in the character dictionary with the slot type as the key
                                    existingCharacter[slotType.Key] = inventoryData;
                                    Console.WriteLine($"Stored {slotType.Key} data for character {characterName}");
                                }
                                catch (JsonSerializationException ex)
                                {
                                    Console.WriteLine($"Failed to deserialize {slotType.Key} data for character {characterName}: {ex.Message}");
                                }
                            }
                            await Task.Delay(4000);
                        }
                        // Process alchemy mixed items
                        var alchemyResponse = await ProcessSingleRequest(
                            "http://path.smokymonkeys.com:8080/kyrill/loadAlchemyMixedItems",
                            new Dictionary<string, string>
                            {
                            { "ver_major", userData.Ver_major },
                            { "ver_minor", userData.Ver_minor },
                            { "ver_rivision", userData.Ver_rivision },
                            { "id", userData.Email },
                            { "sessionKey", userData.SessionKey },
                            { "csKey", characterSessionKey }
                            },
                            userData
                        );

                        if (!string.IsNullOrEmpty(alchemyResponse))
                        {
                            try
                            {
                                var alchemyData = JsonConvert.DeserializeObject<object>(alchemyResponse);
                                existingCharacter["mixedItems"] = alchemyData;
                                Console.WriteLine($"Stored alchemy mixed items for character {characterName}");
                            }
                            catch (JsonSerializationException ex)
                            {
                                Console.WriteLine($"Failed to deserialize alchemy mixed items for character {characterName}: {ex.Message}");
                            }
                        }

                        await Task.Delay(4000);

                        // Process character quests
                        var questsResponse = await ProcessSingleRequest(
                            "http://path.smokymonkeys.com:8080/kyrill/loadCharacterQuests",
                            new Dictionary<string, string>
                            {
                            { "ver_major", userData.Ver_major },
                            { "ver_minor", userData.Ver_minor },
                            { "ver_rivision", userData.Ver_rivision },
                            { "id", userData.Email },
                            { "sessionKey", userData.SessionKey },
                            { "csKey", characterSessionKey }
                            },
                            userData
                        );

                        if (!string.IsNullOrEmpty(questsResponse))
                        {
                            try
                            {
                                var questsData = JsonConvert.DeserializeObject<object>(questsResponse);
                                existingCharacter["quests"] = questsData;
                                Console.WriteLine($"Stored quests data for character {characterName}");
                            }
                            catch (JsonSerializationException ex)
                            {
                                Console.WriteLine($"Failed to deserialize quests data for character {characterName}: {ex.Message}");
                            }
                        }

                        await Task.Delay(4000);
                    }
                }

                await Task.Delay(4000);
            }
            // Save all user data to disk
            try
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                string fileName = $"{userData.Email}_save_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
                string json = JsonConvert.SerializeObject(userData, jsonSettings);
                await File.WriteAllTextAsync(fileName, json);

                Console.WriteLine($"\nSuccessfully saved all user data to {fileName}");
                string dbFilePath = "save_grabber.db";
                if (File.Exists(dbFilePath))
                {
                    File.Delete(dbFilePath); // Remove the database if it already exists
                }

                SQLiteConnection connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;");
                connection.Open();

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
                portrait TEXT,
                hireUseDefaultColor BOOLEAN DEFAULT 0,
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
                slots TEXT,
                lfc INTEGER,
                PRIMARY KEY (userId, characterId, dockType)
            );

            CREATE TABLE character_quests (
                userId INTEGER,
                characterId INTEGER,
                quests TEXT,
                PRIMARY KEY (userId, characterId)
            );

            CREATE TABLE character_craftedItems (
                userId INTEGER,
                characterId INTEGER,
                craftedItems TEXT,
                PRIMARY KEY (userId, characterId)
            );
        ";

                SQLiteCommand createTableCommand = new SQLiteCommand(createTableSql, connection);
                createTableCommand.ExecuteNonQuery();

                // Insert user data into the users table
                string insertUserSql = @"
            INSERT INTO users (email, password, osLang, sysLang, characterLimit, sharedBankCount, scarabGem, verified)
            VALUES (@Email, @Password, @OsLang, @SysLang, @CharacterLimit, @SharedBankCount, @ScarabGem, @Verified);
            SELECT last_insert_rowid();
        ";

                SQLiteCommand insertUserCommand = new SQLiteCommand(insertUserSql, connection);
                insertUserCommand.Parameters.AddWithValue("@Email", userData.Email);
                insertUserCommand.Parameters.AddWithValue("@Password", userData.Password);
                insertUserCommand.Parameters.AddWithValue("@OsLang", userData.OsLang);
                insertUserCommand.Parameters.AddWithValue("@SysLang", userData.UiLang);
                insertUserCommand.Parameters.AddWithValue("@CharacterLimit", userData.CharacterLimit);
                insertUserCommand.Parameters.AddWithValue("@SharedBankCount", userData.SharedBankCount);
                insertUserCommand.Parameters.AddWithValue("@ScarabGem", userData.ScarabGem);
                insertUserCommand.Parameters.AddWithValue("@Verified", userData.Verified);


                long userId = (long)insertUserCommand.ExecuteScalar();

                // Insert character data
                foreach (var character in userData.CharacterList)
                {
                    string insertCharacterSql = @"
                INSERT INTO characters (CsKey, userId, name, race, job, hair, hairColor, facialFileHead, facial, 
                                        lastZoneID, portrait, exp, gold, killCount, deadCount, playTime, bagSlotCount, 
                                        bankPageCount, flagCode1, flagCode2, flagCode3, flagCode4, flagCode5, flagCode6, 
                                        flagCode7, flagCode8, skillTreePayExp, bitCount, bits1, bits2, bits3, bits4, sessionKey)
                VALUES (@CsKey, @UserId, @Name, @Race, @Job, @Hair, @HairColor, @FacialFileHead, @Facial, 
                        @LastZoneID, @Portrait, @Exp, @Gold, @KillCount, @DeadCount, @PlayTime, @BagSlotCount, 
                        @BankPageCount, @FlagCode1, @FlagCode2, @FlagCode3, @FlagCode4, @FlagCode5, @FlagCode6, 
                        @FlagCode7, @FlagCode8, @SkillTreePayExp, @BitCount, @Bits1, @Bits2, @Bits3, @Bits4, @SessionKey);
                SELECT last_insert_rowid();
            ";

                    SQLiteCommand insertCharacterCommand = new SQLiteCommand(insertCharacterSql, connection);
                    insertCharacterCommand.Parameters.AddWithValue("@CsKey", character["sessionKey"]);
                    insertCharacterCommand.Parameters.AddWithValue("@UserId", userId);
                    insertCharacterCommand.Parameters.AddWithValue("@Name", character["name"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Race", character["race"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Job", character["job"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Hair", character["hair"]);
                    insertCharacterCommand.Parameters.AddWithValue("@HairColor", character["hairColor"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FacialFileHead", character["facialFileHead"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Facial", character["facial"]);
                    insertCharacterCommand.Parameters.AddWithValue("@LastZoneID", character["lastZoneID"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Portrait", character["portrait"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Exp", character["exp"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Gold", character["gold"]);
                    insertCharacterCommand.Parameters.AddWithValue("@KillCount", character["killCount"]);
                    insertCharacterCommand.Parameters.AddWithValue("@DeadCount", character["deadCount"]);
                    insertCharacterCommand.Parameters.AddWithValue("@PlayTime", character["playTime"]);
                    insertCharacterCommand.Parameters.AddWithValue("@BagSlotCount", character["bagSlotCount"]);
                    insertCharacterCommand.Parameters.AddWithValue("@BankPageCount", character["bankPageCount"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode1", character["flagCode1"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode2", character["flagCode2"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode3", character["flagCode3"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode4", character["flagCode4"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode5", character["flagCode5"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode6", character["flagCode6"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode7", character["flagCode7"]);
                    insertCharacterCommand.Parameters.AddWithValue("@FlagCode8", character["flagCode8"]);
                    insertCharacterCommand.Parameters.AddWithValue("@SkillTreePayExp", character["skilltreePayExp"]);
                    insertCharacterCommand.Parameters.AddWithValue("@BitCount", character["bitCount"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Bits1", character["bits1"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Bits2", character["bits2"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Bits3", character["bits3"]);
                    insertCharacterCommand.Parameters.AddWithValue("@Bits4", character["bits4"]);
                    insertCharacterCommand.Parameters.AddWithValue("@SessionKey", character["sessionKey"]);

                    long characterId = (long)insertCharacterCommand.ExecuteScalar();

                    // Insert inventory data
                    foreach (var slotType in slotTypeToDockType.Keys)
                    {
                        string insertInventorySql = @"
                    INSERT INTO character_inventory (userId, characterId, dockType, slots, lfc)
                    VALUES (@UserId, @CharacterId, @DockType, @Slots, @Lfc);
                ";

                        SQLiteCommand insertInventoryCommand = new SQLiteCommand(insertInventorySql, connection);
                        insertInventoryCommand.Parameters.AddWithValue("@UserId", userId);
                        insertInventoryCommand.Parameters.AddWithValue("@CharacterId", characterId);
                        insertInventoryCommand.Parameters.AddWithValue("@DockType", slotTypeToDockType[slotType]);
                        insertInventoryCommand.Parameters.AddWithValue("@Slots", JsonConvert.SerializeObject(character[slotType]));
                        insertInventoryCommand.Parameters.AddWithValue("@Lfc", 0); // Placeholder

                        insertInventoryCommand.ExecuteNonQuery();
                    }

                    // Insert crafted items
                    string insertCraftedItemsSql = @"
                INSERT INTO character_craftedItems (userId, characterId, craftedItems)
                VALUES (@UserId, @CharacterId, @CraftedItems);
            ";

                    SQLiteCommand insertCraftedItemsCommand = new SQLiteCommand(insertCraftedItemsSql, connection);
                    insertCraftedItemsCommand.Parameters.AddWithValue("@UserId", userId);
                    insertCraftedItemsCommand.Parameters.AddWithValue("@CharacterId", characterId);
                    insertCraftedItemsCommand.Parameters.AddWithValue("@CraftedItems", JsonConvert.SerializeObject(character["mixedItems"]));

                    insertCraftedItemsCommand.ExecuteNonQuery();

                    // Insert quests
                    string insertQuestsSql = @"
                INSERT INTO character_quests (userId, characterId, quests)
                VALUES (@UserId, @CharacterId, @Quests);
            ";

                    SQLiteCommand insertQuestsCommand = new SQLiteCommand(insertQuestsSql, connection);
                    insertQuestsCommand.Parameters.AddWithValue("@UserId", userId);
                    insertQuestsCommand.Parameters.AddWithValue("@CharacterId", characterId);
                    insertQuestsCommand.Parameters.AddWithValue("@Quests", JsonConvert.SerializeObject(character["quests"]));

                    insertQuestsCommand.ExecuteNonQuery();
                }

                connection.Close();
                Console.WriteLine("Database created and populated successfully.");


            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError saving user data to disk: {ex.Message}");
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }


        // Helper method to process a single request
        private static async Task<string> ProcessSingleRequest(
            string endpoint,
            Dictionary<string, string> requestData,
            UserData userData)
        {
            var encryptedRequestData = MakeAESRequestPostData(requestData);
            var response = await SendPostRequest(endpoint, encryptedRequestData);

            if (response.Length < 40)
            {
                Console.WriteLine($"Received empty response from {endpoint}");
                return "[]";
            }

            var decryptedResponse = AESDecodeResultForOnline(response);
            Console.WriteLine($"Processed request to {endpoint}");
            return decryptedResponse;
        }


        public class RequestHandler
        {
            public string Endpoint { get; set; }
            public Func<UserData, Dictionary<string, string>> PrepareRequestData { get; set; }
            public Action<string, UserData> ProcessResponse { get; set; }
        }

        static string MakeAESRequestPostData(Dictionary<string, string> postData)
        {
            // Serialize the dictionary to a JSON string
            string jsonText = JsonConvert.SerializeObject(postData, Formatting.None, new JsonConverter[] {
                new Newtonsoft.Json.Converters.StringEnumConverter()
            });

            // Generate the session key and IV
            string sessionKey = MakeRandomText(20); // 20-byte session key
            string iv = MakeIV(sessionKey, 16); // 16-byte IV

            // Perform AES encryption
            string encryptedData = EncryptAES256("RQ82EOnVuZZs1nc2_NZAgr18RfrrNVd7", iv, jsonText);

            // Return session key and encrypted data as one combined string
            return sessionKey + encryptedData;
        }

        static string MakeRandomText(int byteCount)
        {
            byte[] array = new byte[byteCount];
            new Random().NextBytes(array);
            StringBuilder builder = new StringBuilder(array.Length * 2);
            foreach (byte b in array)
            {
                builder.AppendFormat("{0:x2}", b);
            }
            return builder.ToString();
        }

        static string MakeIV(string code, int length)
        {
            int codeLength = code.Length;
            if (codeLength > length)
            {
                int num = (codeLength - length) / 2;
                return code.Substring(num, length);
            }
            return code;
        }

        static string EncryptAES256(string key_, string iv_, string encText_)
        {
            try
            {
                using (RijndaelManaged rijndael = new RijndaelManaged())
                {
                    rijndael.Padding = PaddingMode.PKCS7;
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.KeySize = 256;
                    rijndael.BlockSize = 128;

                    byte[] keyBytes = Encoding.UTF8.GetBytes(key_);
                    byte[] ivBytes = Encoding.UTF8.GetBytes(iv_);

                    ICryptoTransform encryptor = rijndael.CreateEncryptor(keyBytes, ivBytes);

                    byte[] textBytes = Encoding.UTF8.GetBytes(encText_);

                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(textBytes, 0, textBytes.Length);
                            csEncrypt.FlushFinalBlock();
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine("Error during AES encryption: " + ex.Message);
                return string.Empty;
            }
        }

        static async Task<string> SendPostRequest(string url, string encryptedRequestData)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "UnityPlayer/5.3.6p8 (http://unity3d.com)");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "identity");
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                client.DefaultRequestHeaders.Add("X-Unity-Version", "5.3.6p8");

                var postData = new Dictionary<string, string>
                {
                    { "req", encryptedRequestData }
                };

                var content = new FormUrlEncodedContent(postData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        static string AESDecodeResultForOnline(string data_)
        {
            string sessionKey = data_.Substring(0, 40);
            string encryptedData = data_.Substring(40);
            string iv = MakeIV(sessionKey, 16);
            return DecryptAES256("RQ82EOnVuZZs1nc2_NZAgr18RfrrNVd7", iv, encryptedData);
        }

        static string DecryptAES256(string key_, string iv_, string encText_)
        {
            try
            {
                using (RijndaelManaged rijndael = new RijndaelManaged())
                {
                    rijndael.Padding = PaddingMode.PKCS7;
                    rijndael.Mode = CipherMode.CBC;
                    rijndael.KeySize = 256;
                    rijndael.BlockSize = 128;

                    byte[] keyBytes = Encoding.UTF8.GetBytes(key_);
                    byte[] ivBytes = Encoding.UTF8.GetBytes(iv_);

                    ICryptoTransform decryptor = rijndael.CreateDecryptor(keyBytes, ivBytes);

                    byte[] encryptedBytes = Convert.FromBase64String(encText_);

                    using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine("Error during AES decryption: " + ex.Message);
                return string.Empty;
            }
        }

        class Request
        {
            public string Endpoint { get; set; }
            public Dictionary<string, string> Data { get; set; }
        }
    }
}
