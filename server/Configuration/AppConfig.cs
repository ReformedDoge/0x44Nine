using Newtonsoft.Json;

namespace Ninelives_Offline.Configuration
{
    public static class AppConfig
    {
        public const string CommonKey = "RQ82EOnVuZZs1nc2_NZAgr18RfrrNVd7";
        public const int SessionKeyLength = 40;

        public static string DbFile { get; private set; } = "";

        private static readonly string ConfigFileName = "config.json";

        public static void LoadOrInitializeConfig()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(exeDir, ConfigFileName);

            try
            {
                if (File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<AppConfigData>(File.ReadAllText(configPath));
                    if (config != null && !string.IsNullOrEmpty(config.DbFile))
                    {
                        DbFile = config.DbFile;
                        Console.WriteLine("Configuration loaded successfully.");
                        return;
                    }
                }

                // Handle first-time setup
                Console.WriteLine("First-time setup: Please select a location to store the server database.");

                string dbFilePath = FilePicker.ShowSaveFileDialog();
                if (string.IsNullOrWhiteSpace(dbFilePath))
                {
                    Console.WriteLine("No file selected. Please select a valid database file location and rerun the program.");
                    Environment.Exit(1); // Halt the script with an error code
                }

                DbFile = dbFilePath;

                // Save the configuration with the selected path
                var newConfig = new AppConfigData
                {
                    Configured = true,
                    DbFile = DbFile
                };

                // Ensure the directory exists before saving
                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(configPath, JsonConvert.SerializeObject(newConfig, Formatting.Indented));
                Console.WriteLine($"Configuration saved to {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while loading or saving configuration: {ex.Message}");
                Environment.Exit(1); // Halt the script with an error code
            }
        }

        private class AppConfigData
        {
            public bool Configured { get; set; }
            public string DbFile { get; set; }
        }
    }
}
