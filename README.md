# 0x44Nine - NineLives Offline

This repository provides everything you need to run **NineLives** in an offline environment. It consists of two main components:

1. **client-patcher** - A tool that patches the game to run offline.
2. **server** - A server that simulates the game’s backend for offline play.

## Getting Started

To have an offline experience with NineLives, follow these steps:

### 1. **Patch the Game (Using the Client Patcher)**

Before you can play the game offline, you must patch it using the **NineLives Client Patcher**. The patcher modifies the game files to allow offline play.

#### Steps:
1. Run the patcher executable and follow the instructions to select your **game folder**.
2. The patcher will modify the necessary files, and after running it once, your game is ready to play offline.

> **Note:** Make sure to **backup the `Ninelives\Ninelives_Data\Managed` folder** if you plan to play the game online again later.

### 2. **Run the Server**

The **NineLives Server** simulates the game’s online environment, allowing you to play offline. You must run the server in order for the patched game to connect to it.

#### Steps:
1. Run the server executable. The first time you run it, the server will ask you for a location to save the **database file** (`.db`). This location will be saved in a `config.json` file in the same directory.
2. Once the server is running, you can launch the patched game and connect to the offline server.

### 3. **Account Verification**

When connecting to the server, use the following code for account verification:

- **Verification Code:** `1`  
  (This code will always be `1` and will be displayed in the server window if you forget it.)

### 4. **Play the Game Offline**

Once the server is running and the game is patched, you can launch the game and enjoy the offline experience!

---

## Important Notes

- **.NET Runtime 8:** Both the patcher and server require the **.NET 8 Runtime**. If you don’t have it installed, you can download it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
---


