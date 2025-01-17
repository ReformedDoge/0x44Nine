using System.Reflection;
using System.Runtime.InteropServices;

// build:
// dotnet publish -c Release -r win-x64 --self-contained True
// dotnet publish -c Release -r win-x86 --self-contained True

namespace Ninelives_Patcher
{
    class Program
    {
        static void Main(string[] args)
        {
            bool running = true;

            while (running)
            {
                Console.Clear();
                Console.WriteLine("Welcome to the Ninelives Patcher!");
                Console.WriteLine("Test Version - Contact: 0x44oge on Discord!");

                Console.WriteLine("1 - Patch the game");
                Console.WriteLine("2 - Exit");
                Console.Write("Please select an option (1 or 2): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("Checking for the game folder containing Ninelives_Data...");

                        // Check if the current directory contains a subdirectory called 'Ninelives_Data'
                        string currentDir = Directory.GetCurrentDirectory();
                        string gameFolder = FindGameFolder(currentDir);

                        if (string.IsNullOrEmpty(gameFolder))
                        {
                            // If the game folder is not found, show the folder picker
                            Console.WriteLine("Could not find 'Ninelives_Data' in the current directory. Please select the game folder.");

                            // Prompt the user to select the game folder
                            gameFolder = FolderPicker.ShowFolderPickerDialog();

                            if (string.IsNullOrEmpty(gameFolder))
                            {
                                Console.WriteLine("No folder selected. Exiting...");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Game folder found: " + gameFolder);
                        }

                        // Ensure the gameFolder path is correct by adding "Ninelives_Data" if necessary
                        string targetDllPathUnityEngine = Path.Combine(gameFolder, "Ninelives_Data", "Managed", "UnityEngine.dll");
                        string targetDllPathUnityScript = Path.Combine(gameFolder, "Ninelives_Data", "Managed", "Assembly-UnityScript.dll");

                        // If the game folder is already pointing to the Ninelives_Data folder, adjust the paths
                        if (gameFolder.EndsWith("Ninelives_Data", StringComparison.OrdinalIgnoreCase))
                        {
                            targetDllPathUnityEngine = Path.Combine(gameFolder, "Managed", "UnityEngine.dll");
                            targetDllPathUnityScript = Path.Combine(gameFolder, "Managed", "Assembly-UnityScript.dll");
                        }

                        // Replace the unpatched DLLs with the patched ones
                        ReplaceDll("UnityEngine.dll", targetDllPathUnityEngine);
                        ReplaceDll("Assembly-UnityScript.dll", targetDllPathUnityScript);

                        Console.WriteLine("Game patched successfully!");
                        Console.WriteLine("Press any key to return to the main menu...");
                        Console.ReadKey();
                        break;

                    case "2":
                        running = false;
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Please select 1 or 2.");
                        break;
                }
            }
        }

        public static void ReplaceDll(string patchedDllName, string targetDllPath)
        {
            try
            {
                byte[] patchedDll = LoadEmbeddedDll(patchedDllName);

                if (File.Exists(targetDllPath))
                {
                    File.Delete(targetDllPath);
                }

                File.WriteAllBytes(targetDllPath, patchedDll);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static byte[] LoadEmbeddedDll(string dllName)
        {
            string resourceName = $"Ninelives_Patcher.PatchedDll.{dllName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Embedded DLL {dllName} not found in resources.");

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        // Method to check if 'Ninelives_Data' exists in the current directory or its subdirectories
        public static string FindGameFolder(string directory)
        {
            string targetFolderName = "Ninelives_Data";
            string[] subDirs = Directory.GetDirectories(directory);

            foreach (var subDir in subDirs)
            {
                if (subDir.EndsWith(targetFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return subDir;
                }
            }
            return null;
        }
    }

    public static class FolderPicker
    {
        // PInvoke declarations for the SHBrowseForFolder function
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, [Out] char[] path);

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        public struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public string pszDisplayName;
            public IntPtr lpszTitle; // This will be an IntPtr
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public uint iImage;
        }

        // Constants for the dialog box
        public const uint BIF_NEWDIALOGSTYLE = 0x0040;
        public const uint BIF_RETURNONLYFSDIRS = 0x0001;

        public static string ShowFolderPickerDialog()
        {
            Console.WriteLine("If you cannot see the folder picker window, please move the patcher to the game root directory and re-run the patcher (make sure to close the current one).");

            BROWSEINFO bi = new BROWSEINFO();
            bi.hwndOwner = GetFocus();
            string title = "Select the game folder containing Ninelives_Data"; // Title for the dialog

            // Allocate memory for the title string in UTF-16
            IntPtr titlePtr = Marshal.StringToHGlobalAuto(title);
            bi.lpszTitle = titlePtr;
            bi.ulFlags = BIF_NEWDIALOGSTYLE | BIF_RETURNONLYFSDIRS;

            try
            {
                IntPtr pidl = SHBrowseForFolder(ref bi);
                if (pidl == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to open folder picker.");
                    LocalFree(titlePtr);  // Free the allocated memory for title
                    return null; // Return null if the folder picker fails to open
                }

                char[] buffer = new char[256];
                if (SHGetPathFromIDList(pidl, buffer))
                {
                    string folderPath = new string(buffer).Trim('\0');
                    LocalFree(titlePtr);  // Free the allocated memory for title
                    return folderPath;    // Return the folder path
                }
                else
                {
                    Console.WriteLine("Failed to get path from IDList.");
                    LocalFree(titlePtr);  // Free the allocated memory for title
                    return null;          // Return null if getting the path fails
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                LocalFree(titlePtr);  // Free the allocated memory for title
                return null;          // Return null in case of an exception
            }
        }
    }
}
