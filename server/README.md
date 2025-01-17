# NineLives Server

The **NineLives Server** simulates an offline server environment for the game. To play the game offline, you must first patch your game using the **NineLives Client Patcher**.

## Important Notes

- **Backup Your Files:** Before patching, make sure you back up the `Ninelives\Ninelives_Data\Managed` folder if you intend to play online again later.
- **.NET Runtime 8:** The server requires the .NET 8 Runtime. If you don't have it installed, you can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

## Instructions

### 1. Patch the Game First

Before you can run the server, you must first patch your game using the **NineLives Client Patcher**. The patcher is required to modify your game files and enable the game to connect to the offline server.

- The patcher can be found in the **`client-patcher/`** subfolder of this repository.
- Follow these steps to patch your game:
  1. Run the **patcher**.
  2. Select your **game folder** when prompted.

> **Note:** You only need to run the patcher **once** to apply the necessary changes. After this, the game will be ready to play offline.

### 2. Run the Server

Once the game is patched, proceed to run the **NineLives Server**.

- When you first run the server, it will ask you for a location to save the database file (a `.db` file).
- This location will be saved in a `config.json` file that will be created in the same directory where the server is run from.

### 3. Account Verification

- The verification code is always `1`. It will also be displayed in the server window if you forget it.

### 4. Start Playing Offline

- Once the server is running, you can start the game and connect to the offline server. You are now ready to enjoy the game in offline mode.

## Troubleshooting

- If the server doesn't run, ensure you have the **.NET Runtime 8** installed on your system. You can download it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
- If you have not patched the game using the **NineLives Client Patcher**, the game will not be able to connect to the offline server.

## Build Instructions

> **Placeholder:** 

