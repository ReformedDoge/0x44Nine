using Newtonsoft.Json;
using Ninelives_Offline.Configuration;
using Ninelives_Offline.Data;
using Ninelives_Offline.Models;
using Ninelives_Offline.Services;
using System.Data.SQLite;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Ninelives_Offline.Controllers
{
    public class AccountController
    {
        private readonly AuthenticationService _authService;
        private readonly CryptographyService _cryptoService;
        private readonly DatabaseService _dbService;
        private readonly SessionService _sessionService;


        public AccountController(
    AuthenticationService authService,
    CryptographyService cryptoService,
    DatabaseService dbService,
    SessionService sessionService)
        {
            _authService = authService;
            _cryptoService = cryptoService;
            _dbService = dbService;
            _sessionService = sessionService;
        }


        public void ProcessCharacterList(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the incoming data to retrieve sessionKey from the client request
                var clientRequest = JsonConvert.DeserializeObject<ClientRequest>(decryptedData);
                string activeSessionKey = clientRequest.SessionKey;

                // Step 1: Validate session and retrieve user ID
                int userId = _sessionService.GetUserIdBySession(activeSessionKey); // Using SessionService to get userId by session key
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + activeSessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Step 2: Retrieve characters for the user
                var characters = GetUserCharacters(userId); // Assuming you have this method for fetching characters

                // Step 3: Prepare response with character data in the required format (array of objects)
                var responseData = characters.Select(character => new
                {
                    idHash = character.Id,
                    name = character.Name,
                    race = character.Race,
                    job = character.Job,
                    hair = character.Hair,
                    hairColor = character.HairColor,
                    facialFileHead = character.FacialFileHead,
                    facial = character.Facial,
                    lastZoneID = character.LastZoneID,
                    portrait = character.Portrait
                });
                string jsonResponse = JsonConvert.SerializeObject(responseData);
                // Step 4: Encrypt and send response
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);

                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing character list: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }


        public void ProcessSaveCharacterItemsDiff(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<SaveCharacterItemsDiffRequest>(decryptedData);

                if (request == null)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid request format.");
                    return;
                }

                request.Slots = JsonConvert.DeserializeObject<SaveCharacterItemsDiffSlots>(request.SlotsString);

                if (request.Slots == null)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "No slots to process.");
                    return;
                }

                int characterId = GetCharacterIdByCsKey(request.CsKey);
                if (characterId == -1)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                // Map slot types to their corresponding dock types
                var slotTypeToDockType = new Dictionary<string, int>
                {
                    { "statsItems", 1 },
                    { "bagItems", 2 },
                    { "skillItems", 3 },
                    { "actionItems", 4 },
                    { "lootItems", 5 },
                    { "alchemyItems", 6 },
                    { "bankItems", 7 },
                    { "inboxItems", 8 },
                    { "shopItems", 9 },
                    { "selledItems", 10 },
                    { "shareItems", 11 },
                    { "shopSGItems", 12 }
                };

                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Process each slot type
                foreach (var slotType in slotTypeToDockType.Keys)
                {
                    // Get the slot value dynamically using reflection
                    var slotValue = typeof(SaveCharacterItemsDiffSlots)
                        .GetProperty(slotType, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?.GetValue(request.Slots);

                    if (slotValue == null)
                    {
                        // If this slot type is null, skip processing
                        continue;
                    }

                    // Determine the dock type for the current slot type
                    int dockType = slotTypeToDockType[slotType];

                    // Convert slot value to JSON
                    string slotsJson = JsonConvert.SerializeObject(slotValue);

                    // Deserialize the slotsJson into a list of items to update
                    var itemsToUpdate = JsonConvert.DeserializeObject<List<Item>>(slotsJson);

                    if (dockType == 11) // Shared items
                    {
                        // Fetch all character IDs belonging to the user

                        var characterIds = GetCharacterIdsByUserId(userId);

                        foreach (var sharedCharacterId in characterIds)
                        {
                            // Fetch the existing inventory for this dock type
                            string foundInventoryString = _dbService.GetInventory(userId, sharedCharacterId, dockType.ToString());
                            List<Item> inventory = foundInventoryString != null
                                ? JsonConvert.DeserializeObject<List<Item>>(foundInventoryString)
                                : new List<Item>();

                            foreach (var newItem in itemsToUpdate)
                            {
                                // Check if the item already exists in the inventory (based on slotNumber)
                                var existingItem = inventory.FirstOrDefault(item => item.SlotNumber == newItem.SlotNumber);

                                if (existingItem != null)
                                {
                                    // Update the existing item's properties
                                    existingItem.ItemID = newItem.ItemID;
                                    existingItem.RandomCode = newItem.RandomCode;
                                    existingItem.ItemCount = newItem.ItemCount;
                                }
                                else
                                {
                                    // Add the new item to the inventory
                                    inventory.Add(newItem);
                                }
                            }

                            // Serialize the updated inventory back to JSON
                            string updatedInventory = JsonConvert.SerializeObject(inventory);

                            if (foundInventoryString == null)
                            {
                                // If no inventory was found, create a new entry
                                CreateInventory(sharedCharacterId, dockType.ToString(), updatedInventory, userId, request.Lfc.ToString());
                            }
                            else
                            {
                                // Otherwise, update the existing inventory
                                UpdateInventory(sharedCharacterId, dockType.ToString(), updatedInventory, userId, request.Lfc.ToString());
                            }
                        }
                    }
                    else
                    {
                        // Fetch the existing inventory for this dock type
                        string foundInventoryString = _dbService.GetInventory(userId, characterId, dockType.ToString());
                        List<Item> inventory = foundInventoryString != null
                            ? JsonConvert.DeserializeObject<List<Item>>(foundInventoryString)
                            : new List<Item>();

                        foreach (var newItem in itemsToUpdate)
                        {
                            // Check if the item already exists in the inventory (based on slotNumber)
                            var existingItem = inventory.FirstOrDefault(item => item.SlotNumber == newItem.SlotNumber);

                            if (existingItem != null)
                            {
                                // Update the existing item's properties
                                existingItem.ItemID = newItem.ItemID;
                                existingItem.RandomCode = newItem.RandomCode;
                                existingItem.ItemCount = newItem.ItemCount;
                            }
                            else
                            {
                                // Add the new item to the inventory
                                inventory.Add(newItem);
                            }
                        }

                        // Serialize the updated inventory back to JSON
                        string updatedInventory = JsonConvert.SerializeObject(inventory);

                        if (foundInventoryString == null)
                        {
                            // If no inventory was found, create a new entry
                            CreateInventory(characterId, dockType.ToString(), updatedInventory, userId, request.Lfc.ToString());
                        }
                        else
                        {
                            // Otherwise, update the existing inventory
                            UpdateInventory(characterId, dockType.ToString(), updatedInventory, userId, request.Lfc.ToString());
                        }
                    }
                }
                ResponseHandler.SendSuccessResponse(context.Response, "Inventory updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing SaveCharacterItemsDiff: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }



        private List<Character> GetUserCharacters(int userId)
        {
            var characters = new List<Character>();

            using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
            {
                conn.Open();
                // SQL query to get characters for the specific userId
                string selectCharactersSql = "SELECT * FROM characters WHERE userId = @UserId";

                using (var cmd = new SQLiteCommand(selectCharactersSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("No characters found for the given userId.");
                        }

                        while (reader.Read())
                        {
                            var character = new Character
                            {
                                Id = Convert.ToInt32(reader["id"]),
                                Name = reader["name"].ToString(),
                                Race = Convert.ToInt32(reader["race"]),
                                Job = Convert.ToInt32(reader["job"]),
                                Hair = Convert.ToInt32(reader["hair"]),
                                HairColor = Convert.ToInt32(reader["hairColor"]),
                                FacialFileHead = reader["facialFileHead"].ToString(),
                                Facial = Convert.ToInt32(reader["facial"]),
                                LastZoneID = Convert.ToInt32(reader["lastZoneID"]),
                                Portrait = reader["portrait"].ToString()
                            };

                            characters.Add(character);
                        }
                    }
                }
            }
            //Console.WriteLine($"Retrieved {characters.Count} characters for userId: {userId}");

            return characters;
        }
        public void ProcessCharacterConnectionKeep(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var userData = JsonConvert.DeserializeObject<UserData>(decryptedData);

                int scarabGem = 0; // Default value if not found
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkEmailSql = "SELECT scarabGem FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkEmailSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", userData.Email);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                scarabGem = int.Parse(reader["scarabGem"].ToString());
                            }
                            else
                            {
                                // Handle case if email is not found in the database
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }
                }

                // Construct the response data including the scarabGem (pcSG)
                var responseData = new
                {
                    isPlay = true,
                    respondTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),  // Current UTC time in milliseconds
                    pcIsEnable = true,
                    pcSG = scarabGem
                };

                // Serialize the response to JSON
                string jsonResponse = JsonConvert.SerializeObject(responseData);
                Console.WriteLine("Character Connection Keep Response Data: " + jsonResponse);

                // Encrypt the response data using AES
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);

                // Send the encrypted response back to the client
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing connectionKeep: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessAddAccount(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var userData = JsonConvert.DeserializeObject<UserData>(decryptedData);

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Check if email already exists
                    string checkEmailSql = "SELECT id FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkEmailSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", userData.Email);
                        object result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            ResponseHandler.SendErrorResponse(context.Response, "Email already in use.");
                            return;
                        }
                    }

                    // Generate verification code
                    string verificationCode = "1"; // On prod replace with_authService.GenerateVerificationCode();

                    // Insert new user with verification code
                    string insertUserSql = @"
                    INSERT INTO users (
                        email, password, sysLang, osLang, 
                        characterLimit, sharedBankCount, scarabGem, 
                        verificationCode, verified
                    ) VALUES (
                        @Email, @Password, @SysLang, @OsLang,
                        @CharacterLimit, @SharedBankCount, @ScarabGem,
                        @VerificationCode, @Verified
                    )";

                    using (var insertCmd = new SQLiteCommand(insertUserSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Email", userData.Email);
                        insertCmd.Parameters.AddWithValue("@Password", userData.Password);
                        insertCmd.Parameters.AddWithValue("@SysLang", userData.SysLang);
                        insertCmd.Parameters.AddWithValue("@OsLang", userData.OsLang);
                        insertCmd.Parameters.AddWithValue("@CharacterLimit", 2); // revert these later
                        insertCmd.Parameters.AddWithValue("@SharedBankCount", 0);
                        insertCmd.Parameters.AddWithValue("@ScarabGem", 50000);
                        insertCmd.Parameters.AddWithValue("@VerificationCode", verificationCode);
                        insertCmd.Parameters.AddWithValue("@Verified", false);
                        insertCmd.ExecuteNonQuery();
                    }

                    // Generate new session
                    string newSessionKey = _authService.GenerateSessionKey();

                    // Insert session record
                    string insertSessionSql = "INSERT INTO sessions (sessionKey, userId) VALUES (@SessionKey, (SELECT id FROM users WHERE email = @Email))";
                    using (var insertSessionCmd = new SQLiteCommand(insertSessionSql, conn))
                    {
                        insertSessionCmd.Parameters.AddWithValue("@SessionKey", newSessionKey);
                        insertSessionCmd.Parameters.AddWithValue("@Email", userData.Email);
                        insertSessionCmd.ExecuteNonQuery();
                    }

                    // Prepare response
                    var responseData = new
                    {
                        characterLimit = 2,
                        sharedBankCount = 0,
                        scarabGem = 50000,
                        sessionKey = newSessionKey
                    };

                    string jsonResponse = JsonConvert.SerializeObject(responseData);
                    Console.WriteLine("Verification Code: " + verificationCode);

                    // Encrypt and send response
                    string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                    ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing addAccount: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }


        public void ProcessLogin(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var userData = JsonConvert.DeserializeObject<UserData>(decryptedData);

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkEmailSql = "SELECT id, password, verified, characterLimit, sharedBankCount, scarabGem FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkEmailSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", userData.Email);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedPassword = reader["password"].ToString();
                                int characterLimit = int.Parse(reader["characterLimit"].ToString());
                                int sharedBankCount = int.Parse(reader["sharedBankCount"].ToString());
                                int scarabGem = int.Parse(reader["scarabGem"].ToString());
                                bool isVerified = Convert.ToBoolean(reader["verified"]);

                                if (!isVerified)
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Account not verified.");
                                    return;
                                }

                                if (storedPassword == userData.Password)
                                {
                                    string newSessionKey = _authService.GenerateSessionKey();
                                    int userId = Convert.ToInt32(reader["id"]);

                                    // Update session
                                    string updateSessionSql = "INSERT OR REPLACE INTO sessions (sessionKey, userId) VALUES (@SessionKey, @UserId)";
                                    using (var sessionCmd = new SQLiteCommand(updateSessionSql, conn))
                                    {
                                        sessionCmd.Parameters.AddWithValue("@SessionKey", newSessionKey);
                                        sessionCmd.Parameters.AddWithValue("@UserId", userId);
                                        sessionCmd.ExecuteNonQuery();
                                    }

                                    var responseData = new
                                    {
                                        characterLimit = characterLimit,
                                        sharedBankCount = sharedBankCount,
                                        scarabGem = scarabGem,
                                        sessionKey = newSessionKey
                                    };

                                    string jsonResponse = JsonConvert.SerializeObject(responseData);
                                    string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                                    ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                                }
                                else
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Invalid credentials.");
                                }
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "Email not found.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing login: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessConnectionKeep(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Construct response data for connectionKeep
                var userData = JsonConvert.DeserializeObject<UserData>(decryptedData);

                int scarabGem = 0; // Default value if not found
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkEmailSql = "SELECT scarabGem FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkEmailSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", userData.Email);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                scarabGem = int.Parse(reader["scarabGem"].ToString());
                            }
                            else
                            {
                                // Handle case if email is not found in the database
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }
                }

                // Construct the response data including the scarabGem (pcSG)
                var responseData = new
                {
                    isPlay = true,
                    respondTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),  // Current UTC time in milliseconds
                    pcIsEnable = true,
                    pcSG = scarabGem
                };

                // Serialize the response to JSON
                string jsonResponse = JsonConvert.SerializeObject(responseData);
                Console.WriteLine("Connection Keep Response Data: " + jsonResponse);

                // Encrypt the response data using AES
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);

                // Send the encrypted response back to the client
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing connectionKeep: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }


        public void ProcessCreateCharacter(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var createCharacterRequest = JsonConvert.DeserializeObject<CreateCharacterRequest>(decryptedData);

                // Extract email (id) from the request
                string clientEmail = createCharacterRequest.Id;

                // Validate session
                int userId = _sessionService.GetUserIdBySession(createCharacterRequest.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + createCharacterRequest.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Check if the character name already exists for this user
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkCharacterNameSql = "SELECT COUNT(*) FROM characters WHERE userId = @UserId AND name = @Name";
                    using (var cmd = new SQLiteCommand(checkCharacterNameSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Name", createCharacterRequest.Name);

                        int existingCount = Convert.ToInt32(cmd.ExecuteScalar());
                        if (existingCount > 0)
                        {
                            ResponseHandler.SendErrorResponse(context.Response, "Character name already exists.");
                            return;
                        }
                    }
                }

                // Fetch characterLimit from database for the user
                int characterLimit = 2; // Default value if not found
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkEmailSql = "SELECT characterLimit FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkEmailSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", clientEmail);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                characterLimit = int.Parse(reader["characterLimit"].ToString());
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }
                }

                // Check character limit
                var currentCharacters = GetUserCharacters(userId);
                if (currentCharacters.Count >= characterLimit)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Character limit reached.");
                    return;
                }

                // Generate unique character session key (csKey)
                string csKey = GenerateCsKey();

                // Create new character
                var newCharacter = new Character
                {
                    Name = createCharacterRequest.Name,
                    Job = createCharacterRequest.Job,
                    Race = createCharacterRequest.Race,
                    Hair = createCharacterRequest.Hair,
                    HairColor = createCharacterRequest.HairColor,
                    FacialFileHead = createCharacterRequest.FacialFileHead,
                    Facial = createCharacterRequest.Facial,
                    LastZoneID = 0,
                    Portrait = string.Empty,
                    HireUseDefaultColor = createCharacterRequest.HireUseDefaultColor,
                    UserId = userId, // Map userId based on session
                    CsKey = csKey // Assign generated csKey
                };

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Insert character into database
                    string insertCharacterSql = @"
                    INSERT INTO characters 
                    (userId, name, csKey, job, race, hair, hairColor, facialFileHead, facial, lastZoneID, portrait, hireUseDefaultColor)
                    VALUES 
                    (@UserId, @Name, @CsKey, @Job, @Race, @Hair, @HairColor, @FacialFileHead, @Facial, @LastZoneID, @Portrait, @HireUseDefaultColor);";

                    using (var cmd = new SQLiteCommand(insertCharacterSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", newCharacter.UserId);
                        cmd.Parameters.AddWithValue("@Name", newCharacter.Name);
                        cmd.Parameters.AddWithValue("@CsKey", newCharacter.CsKey);
                        cmd.Parameters.AddWithValue("@Job", newCharacter.Job);
                        cmd.Parameters.AddWithValue("@Race", newCharacter.Race);
                        cmd.Parameters.AddWithValue("@Hair", newCharacter.Hair);
                        cmd.Parameters.AddWithValue("@HairColor", newCharacter.HairColor);
                        cmd.Parameters.AddWithValue("@FacialFileHead", newCharacter.FacialFileHead);
                        cmd.Parameters.AddWithValue("@Facial", newCharacter.Facial);
                        cmd.Parameters.AddWithValue("@LastZoneID", newCharacter.LastZoneID);
                        cmd.Parameters.AddWithValue("@Portrait", newCharacter.Portrait);
                        cmd.Parameters.AddWithValue("@HireUseDefaultColor", newCharacter.HireUseDefaultColor);

                        cmd.ExecuteNonQuery();
                    }
                }

                // Get the new character ID
                int newCharacterId = GetCharacterIdByCsKey(csKey);

                // Copy shared inventory (dock type 11) from the first character, if it exists
                if (currentCharacters.Count > 0)
                {
                    // Get the first character's ID
                    int firstCharacterId = currentCharacters.First().Id;

                    // Check for dock type 11 inventory for the first character
                    string sharedInventory = _dbService.GetInventory(userId, firstCharacterId, "11");
                    if (!string.IsNullOrEmpty(sharedInventory))
                    {
                        // Copy the shared inventory to the new character
                        CreateInventory(newCharacterId, "11", sharedInventory, userId, "0");
                    }
                }

                // Initialize quests and crafted items for the new character
                _dbService.InitializeCharacterQuests(userId: userId, characterId: newCharacterId);
                _dbService.InitializeCharacterCraftedItems(userId: userId, characterId: newCharacterId);

                // Prepare success response with csKey
                var responseData = new { sessionKey = newCharacter.CsKey };
                string jsonResponse = JsonConvert.SerializeObject(responseData);

                // Encrypt and send the response
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing character creation: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }




        public void ProcessRemoveCharacter(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the incoming request
                var request = JsonConvert.DeserializeObject<CreateCharacterRequest>(decryptedData);

                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                string characterName = request.Name;

                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Fetch the character ID by userId and characterName
                int characterId = _dbService.GetCharacterIdByName(userId, characterName);

                if (characterId == -1)
                {
                    Console.WriteLine("Character not found for userId: " + userId + " and name: " + characterName);
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                // Remove the character's entries from character_inventory and character_quests
                _dbService.RemoveCharacterInventory(characterId);
                _dbService.RemoveCharacterQuests(characterId);
                _dbService.RemoveCharacterCraftedItems(characterId);

                // Remove the character from the characters table
                _dbService.RemoveCharacter(userId, characterId);

                ResponseHandler.SendSuccessResponse(context.Response, "Character successfully removed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in ProcessRemoveCharacter: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }

        // Helper method to generate a unique csKey
        private string GenerateCsKey()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");
        }

        public void ProcessSaveCharacter(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize request
                var request = JsonConvert.DeserializeObject<SaveCharacterRequest>(decryptedData);

                // Validate session key
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Find character by csKey
                var character = _dbService.GetCharacterByCsKey(request.CsKey);
                if (character == null)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                // Update character properties dynamically from request fields
                UpdateCharacterPropertyIfNotNull(request.LastZoneID, value => character.LastZoneID = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Exp, value => character.Exp = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Gold, value => character.Gold = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.KillCount, value => character.KillCount = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.DeadCount, value => character.DeadCount = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.PlayTime, value => character.PlayTime = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.UserId, value => character.UserId = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Name, value => character.Name = value);
                UpdateCharacterPropertyIfNotNull(request.Race, value => character.Race = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Job, value => character.Job = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Hair, value => character.Hair = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.HairColor, value => character.HairColor = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.FacialFileHead, value => character.FacialFileHead = value);
                UpdateCharacterPropertyIfNotNull(request.Facial, value => character.Facial = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Portrait, value => character.Portrait = value);
                UpdateCharacterPropertyIfNotNull(request.HireUseDefaultColor, value => character.HireUseDefaultColor = bool.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.BagSlotCount, value => character.BagSlotCount = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.BankPageCount, value => character.BankPageCount = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.SkillTreePayExp, value => character.SkillTreePayExp = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.BitCount, value => character.BitCount = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Bits1, value => character.Bits1 = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Bits2, value => character.Bits2 = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Bits3, value => character.Bits3 = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.Bits4, value => character.Bits4 = int.Parse(value));
                UpdateCharacterPropertyIfNotNull(request.SessionKey, value => character.SessionKey = value);
                UpdateCharacterPropertyIfNotNull(request.CsKey, value => character.CsKey = value);
                // flags
                UpdateCharacterPropertyIfNotNull(request.FlagCode1, value => character.FlagCode1 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode2, value => character.FlagCode2 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode3, value => character.FlagCode3 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode4, value => character.FlagCode4 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode5, value => character.FlagCode5 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode6, value => character.FlagCode6 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode7, value => character.FlagCode7 = value);
                UpdateCharacterPropertyIfNotNull(request.FlagCode8, value => character.FlagCode8 = value);

                // Save updates to the database
                if (!_dbService.SaveCharacter(character))
                {
                    Console.WriteLine("Failed to save character.");
                    ResponseHandler.SendErrorResponse(context.Response, "Failed to save character.");
                    return;
                }

                // Respond with success (0)
                string responseText = "0";
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving character: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }

        private void UpdateCharacterPropertyIfNotNull(string value, Action<string> updateAction)
        {
            if (!string.IsNullOrEmpty(value))
            {
                updateAction(value);
            }
        }
        public void ProcessSaveCharacterPortrait(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            ProcessSaveCharacter(decryptedData, sessionKey, context);
        }
        public void ProcessLoadCharacter(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize request
                var request = JsonConvert.DeserializeObject<LoadCharacterRequest>(decryptedData);

                // Validate session key
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Check if character belongs to this user and matches name
                var character = _dbService.GetCharacterByUserIdAndName(userId, request.Name);
                character.CsKey = GenerateCsKey();
                bool saveResult = _dbService.SaveCharacter(character);

                if (character == null)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                // Proceed with loading character data
                string jsonResponse = JsonConvert.SerializeObject(character);
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);

                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading character: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }

        public void ProcessLoadCharacterItems(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the decrypted request data
                var request = JsonConvert.DeserializeObject<SaveCharacterItemsRequest>(decryptedData);

                string csKey = request.CsKey;  // Character session key
                string dockType = request.DockType;
                int characterId = GetCharacterIdByCsKey(csKey);
                if (characterId == -1)
                {
                    // Character not found for the given csKey
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    // Invalid session
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }
                string inventory = _dbService.GetInventory(userId, characterId, dockType);

                // Encrypt and send response
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, inventory);
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing request: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }
        public void ProcessSaveCharacterItems(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the decrypted request data
                var request = JsonConvert.DeserializeObject<SaveCharacterItemsRequest>(decryptedData);

                string csKey = request.CsKey;  // Character session key
                string dockType = request.DockType;
                string slotsJson = request.Slots;  // The JSON string of slots provided by the client

                // Get the characterId based on the csKey
                int characterId = GetCharacterIdByCsKey(csKey);
                if (characterId == -1)
                {
                    // Character not found for the given csKey
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    // Invalid session
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Get lfc value (this can be passed via the request or fetched based on business logic)
                string lfc = request.Lfc;

                // Check if inventory exists for this character and dockType
                bool inventoryExists = CheckIfInventoryExists(characterId, dockType);
                if (inventoryExists)
                {
                    // Inventory exists, so we update it
                    UpdateInventory(characterId, dockType, slotsJson, userId, lfc);
                }
                else
                {
                    // Inventory does not exist, so we create a new one
                    CreateInventory(characterId, dockType, slotsJson, userId, lfc);
                }

                // Send the response with a single character '0'
                string responseText = "0";
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                // Log the error (you can also send an error response if needed)
                Console.WriteLine("Error processing request: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Internal Server Error");
            }
        }




        private List<int> GetCharacterIdsByUserId(int userId)
        {
            var characterIds = new List<int>();
            using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
            {
                conn.Open();

                string sql = "SELECT id FROM characters WHERE userId = @UserId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            characterIds.Add(Convert.ToInt32(reader["id"]));
                        }
                    }
                }
            }
            return characterIds;
        }

        // Helper method to get characterId by csKey
        private int GetCharacterIdByCsKey(string csKey)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();
                    string sql = "SELECT id FROM characters WHERE csKey = @CsKey";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CsKey", csKey);
                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : -1;  // Return -1 if not found
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving id: " + ex.Message);
                return -1;
            }
        }

        // Helper method to check if inventory exists for the character
        private bool CheckIfInventoryExists(int characterId, string dockType)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM character_inventory WHERE characterId = @CharacterId AND dockType = @DockType";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        cmd.Parameters.AddWithValue("@DockType", dockType);
                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error checking inventory: " + ex.Message);
                return false;
            }
        }

        // Helper method to update the inventory for the character
        private void UpdateInventory(int characterId, string dockType, string slotsJson, int userId, string lfc)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();
                    string sql = @"
                    UPDATE character_inventory
                    SET slots = @Slots, userId = @UserId, lfc = @Lfc
                    WHERE characterId = @CharacterId AND dockType = @DockType;
                    ";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        cmd.Parameters.AddWithValue("@DockType", dockType);
                        cmd.Parameters.AddWithValue("@Slots", slotsJson);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Lfc", lfc);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating inventory: " + ex.Message);
            }
        }


        // Helper method to create a new inventory for the character
        private void CreateInventory(int characterId, string dockType, string slotsJson, int userId, string lfc)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();
                    string sql = @"
                INSERT INTO character_inventory (characterId, dockType, slots, userId, lfc)
                VALUES (@CharacterId, @DockType, @Slots, @UserId, @Lfc);
            ";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        cmd.Parameters.AddWithValue("@DockType", dockType);
                        cmd.Parameters.AddWithValue("@Slots", slotsJson);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Lfc", lfc);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating inventory: " + ex.Message);
            }
        }

        public void ProcessLoadCharacterQuests(string decryptedData, string sessionKey, HttpListenerContext context)
        {

            var request = JsonConvert.DeserializeObject<CharacterQuestRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            string questsJson = _dbService.GetCharacterQuests(characterId: characterId);
            string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, questsJson);
            ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);

        }
        public void ProcessCompleteCharacterQuest(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<CharacterQuestRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            int QuestId = request.QuestID;
            if (request.IsRequestRemove ?? false)
            {
                _dbService.RemoveQuestFromCharacter(characterId: characterId, questId: QuestId);
            }
            string responseText = "0";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }
        public void ProcessRemoveCharacterQuest(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<CharacterQuestRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            int QuestId = request.QuestID;
            _dbService.RemoveQuestFromCharacter(characterId: characterId, questId: QuestId);
            string responseText = "0";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();

        }
        public void ProcessLoadAlchemyRecipeList(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the incoming request
                var request = JsonConvert.DeserializeObject<LoadAlchemyRecipeListRequest>(decryptedData);

                int vendorID = int.Parse(request.VendorID);
                int keyItemId = int.Parse(request.KeyItemId);

                // Use the RecipeData class to get matching recipes
                var matchingRecipes = RecipeData.GetRecipesForVendor(vendorID.ToString(), keyItemId);

                // Serialize the result
                string resultJson = JsonConvert.SerializeObject(matchingRecipes, Formatting.None);
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, resultJson);

                // If no matches are found, return an empty response
                if (!matchingRecipes.Any())
                {
                    Console.WriteLine($"No matching recipes found for KeyItemID {keyItemId} under Vendor ID {vendorID}.");
                }
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }
        public void ProcessLoadAlchemyRecipeCode(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize the incoming request
                var request = JsonConvert.DeserializeObject<LoadAlchemyRecipeListRequest>(decryptedData);

                int vendorID = int.Parse(request.VendorID);
                int recipeID = int.Parse(request.RecipeID);
                int keyItemId = int.Parse(request.KeyItemId);

                // Use the RecipeData class to get matching recipes for vendorID and keyItemId
                var matchingRecipes = RecipeData.GetRecipesForVendor(vendorID.ToString(), keyItemId);

                // Filter further by recipeID
                var recipeMatch = matchingRecipes.FirstOrDefault(recipe => recipe.RecipeID == recipeID);

                // If no match is found, return an empty response
                if (recipeMatch == null)
                {
                    Console.WriteLine($"No matching recipe found for RecipeID {recipeID}, KeyItemID {keyItemId}, under Vendor ID {vendorID}.");
                    ResponseHandler.SendSuccessResponse(context.Response, CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, "[]"));
                    return;
                }

                // Generate the recipe code for the matching recipe
                var recipeCode = GenerateRecipeCode(vendorID, recipeMatch);

                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, recipeCode);

                // Send the response back to the client
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }
        public void ProcessBagUnlockBySG(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize incoming request
                var request = JsonConvert.DeserializeObject<BagUnlockBySGRequest>(decryptedData);

                // Validate session and retrieve userId
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Retrieve character details using csKey
                int characterId = GetCharacterIdByCsKey(request.CsKey);
                if (characterId == -1)
                {
                    Console.WriteLine("Character not found with csKey: " + request.CsKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Retrieve user's scarabGem
                    int scarabGem = 0;
                    string getUserDataSql = "SELECT scarabGem FROM users WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(getUserDataSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                scarabGem = Convert.ToInt32(reader["scarabGem"]);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }

                    // Retrieve character's current bagSlotCount
                    int bagSlotCount = 0;
                    string getCharacterDataSql = "SELECT bagSlotCount FROM characters WHERE id = @CharacterId";
                    using (var cmd = new SQLiteCommand(getCharacterDataSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                bagSlotCount = Convert.ToInt32(reader["bagSlotCount"]);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                                return;
                            }
                        }
                    }

                    // Define the cost and max bag slot count
                    const int cost = 60;
                    const int maxBagSlotCount = 42;
                    const int increment = 6;

                    // Check if the user can afford the cost and if there's room to unlock more slots
                    if (scarabGem < cost)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Insufficient scarab gems.");
                        return;
                    }

                    if (bagSlotCount >= maxBagSlotCount)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Bag slots already at maximum.");
                        return;
                    }

                    // Perform the update: deduct scarab gems, increase bag slot count
                    scarabGem -= cost;
                    bagSlotCount = Math.Min(bagSlotCount + increment, maxBagSlotCount);

                    // Update user scarabGem
                    string updateUserSql = "UPDATE users SET scarabGem = @ScarabGem WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(updateUserSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ScarabGem", scarabGem);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // Update character bagSlotCount
                    string updateCharacterSql = "UPDATE characters SET bagSlotCount = @BagSlotCount WHERE id = @CharacterId";
                    using (var cmd = new SQLiteCommand(updateCharacterSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BagSlotCount", bagSlotCount);
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        cmd.ExecuteNonQuery();
                    }

                    // Prepare and send the response
                    var responseData = new
                    {
                        scarabGem = scarabGem,
                        bagSlotCount = bagSlotCount,
                    };
                    string jsonResponse = JsonConvert.SerializeObject(responseData);
                    string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                    ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing bag unlock: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessBankUnlockBySG(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize incoming request
                var request = JsonConvert.DeserializeObject<BankUnlockBySGRequest>(decryptedData);

                // Validate session and retrieve userId
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Retrieve character details using csKey
                int characterId = GetCharacterIdByCsKey(request.CsKey);
                if (characterId == -1)
                {
                    Console.WriteLine("Character not found with csKey: " + request.CsKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                    return;
                }

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Retrieve user's scarabGem
                    int scarabGem = 0;
                    string getUserDataSql = "SELECT scarabGem FROM users WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(getUserDataSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                scarabGem = Convert.ToInt32(reader["scarabGem"]);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }

                    // Retrieve character's current bankPageCount
                    int bankPageCount = 0;
                    string getCharacterDataSql = "SELECT bankPageCount FROM characters WHERE id = @CharacterId";
                    using (var cmd = new SQLiteCommand(getCharacterDataSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                bankPageCount = Convert.ToInt32(reader["bankPageCount"]);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                                return;
                            }
                        }
                    }

                    // Define the cost and max bank page count
                    const int cost = 50;
                    const int maxPageCount = 10;

                    // Check if the user can afford the cost and if there's room to unlock more pages
                    if (scarabGem < cost)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Insufficient scarab gems.");
                        return;
                    }

                    if (bankPageCount >= maxPageCount)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Bank pages already at maximum.");
                        return;
                    }

                    // Perform the update: deduct scarab gems, increase bank page count
                    scarabGem -= cost;
                    bankPageCount = Math.Min(bankPageCount + 1, maxPageCount);

                    // Update user scarabGem
                    string updateUserSql = "UPDATE users SET scarabGem = @ScarabGem WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(updateUserSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ScarabGem", scarabGem);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // Update character bankPageCount
                    string updateCharacterSql = "UPDATE characters SET bankPageCount = @BankPageCount WHERE id = @CharacterId";
                    using (var cmd = new SQLiteCommand(updateCharacterSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BankPageCount", bankPageCount);
                        cmd.Parameters.AddWithValue("@CharacterId", characterId);
                        cmd.ExecuteNonQuery();
                    }

                    // Prepare and send the response
                    var responseData = new
                    {
                        scarabGem = scarabGem,
                        bankPageCount = bankPageCount,
                    };
                    string jsonResponse = JsonConvert.SerializeObject(responseData);
                    string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                    ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing bank unlock: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessAddSharedBank(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Deserialize incoming request
                var request = JsonConvert.DeserializeObject<AddSharedBankRequest>(decryptedData);

                // Validate session and retrieve userId
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    // Retrieve user's scarabGem and sharedBankCount
                    int scarabGem = 0;
                    int sharedBankCount = 0;
                    const int maxSharedBankCount = 10; // maximum shared bank slots
                    const int sharedBankUnlockCost = 150; // cost to unlock one shared bank slot

                    string getUserDataSql = "SELECT scarabGem, sharedBankCount FROM users WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(getUserDataSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                scarabGem = Convert.ToInt32(reader["scarabGem"]);
                                sharedBankCount = Convert.ToInt32(reader["sharedBankCount"]);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }

                    // Check if the user can afford the cost and if there's room for more shared bank slots
                    if (scarabGem < sharedBankUnlockCost)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Insufficient scarab gems.");
                        return;
                    }

                    if (sharedBankCount >= maxSharedBankCount)
                    {
                        ResponseHandler.SendErrorResponse(context.Response, "Shared bank slots already at maximum.");
                        return;
                    }

                    // Deduct scarab gems and increment sharedBankCount
                    scarabGem -= sharedBankUnlockCost;
                    sharedBankCount++;

                    // Update user data in the database
                    string updateUserSql = "UPDATE users SET scarabGem = @ScarabGem, sharedBankCount = @SharedBankCount WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(updateUserSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ScarabGem", scarabGem);
                        cmd.Parameters.AddWithValue("@SharedBankCount", sharedBankCount);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // Prepare and send the response
                    var responseData = new
                    {
                        sharedBank = sharedBankCount,
                        scarabGem = scarabGem,
                    };
                    string jsonResponse = JsonConvert.SerializeObject(responseData);
                    string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                    ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing shared bank unlock: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }


        public void ProcessRMResetSkilltree(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                int resetBaseCost = 5; // Base cost per reset
                int totalSGCost = resetBaseCost * 10; // Total cost for resetting the skill tree

                // Deserialize the request to get session key
                var request = JsonConvert.DeserializeObject<ResetSkillTreeRequest>(decryptedData);

                // Validate session key and get user ID
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Get the user's current scarabGem (SG) from the database
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string getSGSql = "SELECT scarabGem FROM users WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(getSGSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int scarabGem = int.Parse(reader["scarabGem"].ToString());

                                // Check if the user has enough scarab gems
                                if (scarabGem < totalSGCost)
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Not enough scarab gems to reset the skill tree.");
                                    return;
                                }

                                // Deduct the total cost from scarabGem
                                scarabGem -= totalSGCost;

                                // Update the user's scarabGem in the database
                                string updateSGSql = "UPDATE users SET scarabGem = @ScarabGem WHERE id = @UserId";
                                using (var updateCmd = new SQLiteCommand(updateSGSql, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@ScarabGem", scarabGem);
                                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                                    updateCmd.ExecuteNonQuery();
                                }

                                // Respond with the updated scarabGem value
                                var responseData = new { pcSG = scarabGem };
                                string jsonResponse = JsonConvert.SerializeObject(responseData);
                                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing reset skill tree: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessAddCharacterLimit(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                // Maximum character slot limit and cost per slot
                int characterSlotLimitMax = 6;
                int addCharacterSlotCost = 50;

                // Deserialize incoming request
                var request = JsonConvert.DeserializeObject<AddCharacterLimitRequest>(decryptedData);

                // Validate session key and get user ID
                int userId = _sessionService.GetUserIdBySession(request.SessionKey);
                if (userId == -1)
                {
                    Console.WriteLine("Invalid session key: " + request.SessionKey);
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                    return;
                }

                // Get the user's current characterLimit and scarabGem
                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string getUserSql = "SELECT characterLimit, scarabGem FROM users WHERE id = @UserId";
                    using (var cmd = new SQLiteCommand(getUserSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int characterLimit = int.Parse(reader["characterLimit"].ToString());
                                int scarabGem = int.Parse(reader["scarabGem"].ToString());

                                // Check if the user has reached the maximum character slot limit
                                if (characterLimit >= characterSlotLimitMax)
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Character slot limit reached.");
                                    return;
                                }

                                // Check if the user has enough scarab gems to add a slot
                                if (scarabGem < addCharacterSlotCost)
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Not enough scarab gems to add a character slot.");
                                    return;
                                }

                                // Deduct scarab gems and increase characterLimit
                                scarabGem -= addCharacterSlotCost;
                                characterLimit++;

                                // Update the database with the new characterLimit and scarabGem
                                string updateUserSql = "UPDATE users SET characterLimit = @CharacterLimit, scarabGem = @ScarabGem WHERE id = @UserId";
                                using (var updateCmd = new SQLiteCommand(updateUserSql, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@CharacterLimit", characterLimit);
                                    updateCmd.Parameters.AddWithValue("@ScarabGem", scarabGem);
                                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                                    updateCmd.ExecuteNonQuery();
                                }

                                // Respond with the updated characterLimit and scarabGem
                                var responseData = new
                                {
                                    characterCountLimit = characterLimit,
                                    scarabGem = scarabGem
                                };
                                string jsonResponse = JsonConvert.SerializeObject(responseData);
                                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
                                return;
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "User not found.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing add character limit: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }

        public void ProcessLoadInboxItems(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<LoadInboxItemsRequest>(decryptedData);

            // Create a list of response objects for testing
            var responseDataList = new List<object>
            {
                new
                {
                    id = 1,
                    isCommon = true,
                    itemID = 800000000,
                    randomCode = -71450918,
                    cnt = 40
                }
            };

            // Serialize the list of objects to JSON
            string jsonResponse = JsonConvert.SerializeObject(responseDataList);

            // Encrypt the response
            string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);

            // Send the encrypted response
            ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
        }

        public void ProcessBankUnlockByItem(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<BankUnlockByItemRequest>(decryptedData);
            int userId = _sessionService.GetUserIdBySession(request.SessionKey);
            if (userId == -1)
            {
                Console.WriteLine("Invalid session key: " + request.SessionKey);
                ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                return;
            }
            int characterId = GetCharacterIdByCsKey(request.CsKey);
            if (characterId == -1)
            {
                Console.WriteLine("Character not found with csKey: " + request.CsKey);
                ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                return;
            }

            using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
            {
                conn.Open();

                // Retrieve character's current bankPageCount
                int bankPageCount = 0;
                const int maxPageCount = 10;
                string getCharacterDataSql = "SELECT bankPageCount FROM characters WHERE id = @CharacterId";
                using (var cmd = new SQLiteCommand(getCharacterDataSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CharacterId", characterId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bankPageCount = Convert.ToInt32(reader["bankPageCount"]);
                        }
                        else
                        {
                            ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                            return;
                        }
                    }
                }

                if (bankPageCount >= maxPageCount)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Bank pages already at maximum.");
                    return;
                }

                bankPageCount = Math.Min(bankPageCount + 1, maxPageCount);

                // Update character bankPageCount
                string updateCharacterSql = "UPDATE characters SET bankPageCount = @BankPageCount WHERE id = @CharacterId";
                using (var updateCmd = new SQLiteCommand(updateCharacterSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@BankPageCount", bankPageCount);
                    updateCmd.Parameters.AddWithValue("@CharacterId", characterId);
                    updateCmd.ExecuteNonQuery();
                }

                // Response 
                var responseDataList = new List<object>
            {
                new
                {
                    id = 1,
                    isCommon = true,
                    itemID = 800000000,
                    randomCode = -71450918,
                    cnt = 40
                }
            };
                var responseData = new
                {
                    bankPageCount = bankPageCount,
                    inboxSlots = responseDataList,
                };
                string jsonResponse = JsonConvert.SerializeObject(responseData);
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, jsonResponse);
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }
        }

        public void ProcessSaveCharacterBagBankCount(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<SaveCharacterBagBankCountRequest>(decryptedData);
            int userId = _sessionService.GetUserIdBySession(request.SessionKey);
            if (userId == -1)
            {
                Console.WriteLine("Invalid session key: " + request.SessionKey);
                ResponseHandler.SendErrorResponse(context.Response, "Invalid session.");
                return;
            }

            // Retrieve character details using csKey
            int characterId = GetCharacterIdByCsKey(request.CsKey);
            if (characterId == -1)
            {
                Console.WriteLine("Character not found with csKey: " + request.CsKey);
                ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                return;
            }
            using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
            {
                conn.Open();
                // Retrieve character's current bagSlotCount
                int bagSlotCount = 0;
                string getCharacterDataSql = "SELECT bagSlotCount FROM characters WHERE id = @CharacterId";
                using (var cmd = new SQLiteCommand(getCharacterDataSql, conn))
                {
                    cmd.Parameters.AddWithValue("@CharacterId", characterId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bagSlotCount = Convert.ToInt32(reader["bagSlotCount"]);
                        }
                        else
                        {
                            ResponseHandler.SendErrorResponse(context.Response, "Character not found.");
                            return;
                        }
                    }
                }
                if (bagSlotCount != 24)
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Bag slots already at maximum.");
                    return;
                }
                bagSlotCount = 30;
                // Update character bagSlotCount
                string updateCharacterSql = "UPDATE characters SET bagSlotCount = @BagSlotCount WHERE id = @CharacterId";
                using (var cmd = new SQLiteCommand(updateCharacterSql, conn))
                {
                    cmd.Parameters.AddWithValue("@BagSlotCount", bagSlotCount);
                    cmd.Parameters.AddWithValue("@CharacterId", characterId);
                    cmd.ExecuteNonQuery();
                }
                string responseText = "0";
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();
            }

        }
        public void ProcessBoxRequest(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<DropRequest>(decryptedData);
            int dropId = int.Parse(request.DropID);
            var dataList = RandomItemIDs.GetRandomItemIDsFromData(dropId);
            string boxResponsejson = JsonConvert.SerializeObject(dataList, Formatting.None);
            string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, boxResponsejson);
            ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
        }
        public void ProcessItemDropRequest(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<DropRequest>(decryptedData);
            int dropId = int.Parse(request.DropID);
            var dataList = RandomItemIDs.ChestDrop(dropId);
            if (dataList.Count == 0)
            {
                string responseText = "0";
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();

            }
            else
            {
                string boxResponsejson = JsonConvert.SerializeObject(dataList, Formatting.None);
                string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, boxResponsejson);
                ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
            }

        }
        public void ProcessShopItemRequest(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<DropRequest>(decryptedData);
            int dropId = int.Parse(request.DropID);
            var dataList = RandomItemIDs.GetRandomItemIDsFromData(dropId, true);
            string boxResponsejson = JsonConvert.SerializeObject(dataList, Formatting.None);
            string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, boxResponsejson);
            ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);

        }
        public void ProcessloadAlchemyMixedItems(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<AlchemyMixedItemRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            string craftedItemssJson = _dbService.GetCharacterCraftedItems(characterId: characterId);
            if (string.IsNullOrEmpty(craftedItemssJson))
            {
                craftedItemssJson = "[]"; // Default to an empty JSON object
            }
            string encryptedResponse = CryptographyService.EncryptAES256(AppConfig.CommonKey, sessionKey, craftedItemssJson);
            ResponseHandler.SendSuccessResponse(context.Response, encryptedResponse);
        }
        public void ProcessSaveAlchemyMixedItem(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            var request = JsonConvert.DeserializeObject<AlchemyMixedItemRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            string recipeID = request.RecipeID;
            _dbService.AddCraftedItemToCharacter(characterId: characterId, craftedItem: recipeID);
            // live version bug fix needed here (ignore for now, will work on it later)

            string responseText = "0";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }

        private string GenerateRecipeCode(int vendorID, Recipe recipe)
        {
            var stringBuilder = new StringBuilder();

            // Add the necessary fields to the code in the correct order
            stringBuilder.Append("recipe_correct_");
            stringBuilder.Append(vendorID);
            stringBuilder.Append(recipe.RecipeID);
            stringBuilder.Append(recipe.KeyItemID);
            stringBuilder.Append(recipe.KeyItemCount);
            stringBuilder.Append(recipe.ResultItemID);
            stringBuilder.Append(recipe.ResultItemCount);

            // Iterate through the materials and append their data
            foreach (var material in recipe.Mats)
            {
                stringBuilder.Append(material.Id);
                stringBuilder.Append(material.Count);
            }

            // Compute the MD5 hash of the constructed string
            return ComputeMd5Hash(stringBuilder.ToString());
        }


        private string ComputeMd5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }


        public void ProcessSaveCharacterQuest(string decryptedData, string sessionKey, HttpListenerContext context)
        {

            var request = JsonConvert.DeserializeObject<CharacterQuestRequest>(decryptedData);
            var CsKey = request.CsKey;
            int characterId = GetCharacterIdByCsKey(CsKey);
            int QuestId = request.QuestID;
            _dbService.AddQuestToCharacter(characterId: characterId, questId: QuestId);
            string responseText = "0";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }


        public void ProcessVerifyAccount(string decryptedData, string sessionKey, HttpListenerContext context)
        {
            try
            {
                var verifyData = JsonConvert.DeserializeObject<VerifyAccountData>(decryptedData);

                using (var conn = new SQLiteConnection($"Data Source={AppConfig.DbFile};Version=3;"))
                {
                    conn.Open();

                    string checkCodeSql = "SELECT verificationCode, verified FROM users WHERE email = @Email";
                    using (var cmd = new SQLiteCommand(checkCodeSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", verifyData.Email);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedCode = reader["verificationCode"].ToString();
                                bool isVerified = Convert.ToBoolean(reader["verified"]);

                                if (isVerified)
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Account already verified.");
                                    return;
                                }

                                if (storedCode == verifyData.VerificationCode)
                                {
                                    string updateVerifiedSql = "UPDATE users SET verified = 1 WHERE email = @Email";
                                    using (var updateCmd = new SQLiteCommand(updateVerifiedSql, conn))
                                    {
                                        updateCmd.Parameters.AddWithValue("@Email", verifyData.Email);
                                        updateCmd.ExecuteNonQuery();
                                    }

                                    ResponseHandler.SendSuccessResponse(context.Response, "Account verified successfully.");
                                    Console.WriteLine("Account verified successfully.");
                                }
                                else
                                {
                                    ResponseHandler.SendErrorResponse(context.Response, "Invalid verification code.");
                                }
                            }
                            else
                            {
                                ResponseHandler.SendErrorResponse(context.Response, "Email not found.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing verifyAccount: " + ex.Message);
                ResponseHandler.SendErrorResponse(context.Response, "Error processing request.");
            }
        }
    }
}
