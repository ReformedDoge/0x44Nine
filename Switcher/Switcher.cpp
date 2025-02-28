#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>
#include <fstream>
#include <string>
#include <uxtheme.h>
#include <dwmapi.h>

#pragma comment(lib, "uxtheme.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "comctl32.lib")

#define WM_UPDATE_STATUS (WM_USER + 1)

class ModernHostSwitcher {
private:
    static constexpr const wchar_t* HOSTS_FILE = L"C:\\Windows\\System32\\drivers\\etc\\hosts";
    static constexpr const wchar_t* REDIRECT_ENTRY = L"127.0.0.1\tpath.smokymonkeys.com";

    HWND mainWindow;
    HWND localPlayButton;
    HWND onlinePlayButton;
    HFONT hTitleFont;
    bool isLocalPlay = false;
    std::wstring currentStatus = L"Select Play Mode";

    static bool ModifyHostsFile(bool addEntry) {
        std::wifstream hostsFileIn(HOSTS_FILE);
        if (!hostsFileIn.is_open()) return false;

        std::wstring line, hostsContents;
        bool entryExists = false;

        while (std::getline(hostsFileIn, line)) {
            if (line.find(REDIRECT_ENTRY) != std::wstring::npos) {
                entryExists = true;
                if (!addEntry) continue;
            }
            hostsContents += line + L"\n";
        }
        hostsFileIn.close();

        if (addEntry && !entryExists)
            hostsContents.append(REDIRECT_ENTRY).append(L"\n");
        else if (!addEntry && !entryExists)
            return true;

        std::wofstream hostsFileOut(HOSTS_FILE, std::ios::trunc);
        if (!hostsFileOut.is_open()) return false;

        hostsFileOut << hostsContents;
        hostsFileOut.close();
        return true;
    }

    static void FlushDNS() {
        // Create a command to run in cmd
        const wchar_t* command = L"cmd.exe";
        const wchar_t* args = L"/C ipconfig /flushdns";  // /C means "close cmd after execution"

        // Run the command as administrator (elevated)
        HINSTANCE result = ShellExecute(
            NULL,          // Parent window handle
            L"runas",      // Verb for elevated privileges (run as admin)
            command,       // Program to run (cmd.exe)
            args,          // Arguments (the command to execute)
            NULL,          // Current directory
            SW_HIDE        // Hide the cmd window
        );

        // Check if the result indicates failure (ShellExecute returns <= 32 on failure)
        if ((int)result <= 32) {
            MessageBox(NULL, L"Failed to flush DNS. Please ensure you have administrator privileges.", L"Error", MB_OK | MB_ICONERROR);
        }
    }


    void CreateCustomFonts() {
        LOGFONT lfTitle = {};
        lfTitle.lfHeight = -18;
        lfTitle.lfWeight = FW_SEMIBOLD;
        wcscpy_s(lfTitle.lfFaceName, L"Segoe UI");

        hTitleFont = CreateFontIndirect(&lfTitle);
    }

    void UpdateStatusText(const std::wstring& text) {
        currentStatus = text;
        InvalidateRect(mainWindow, NULL, TRUE); // trigger repaint of the window
    }

    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
        ModernHostSwitcher* app = reinterpret_cast<ModernHostSwitcher*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));

        switch (uMsg) {
        case WM_NCCREATE: {
            CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lParam);
            app = static_cast<ModernHostSwitcher*>(pCreate->lpCreateParams);
            SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(app));
            return DefWindowProc(hwnd, uMsg, wParam, lParam);
        }
        case WM_COMMAND: {
            switch (LOWORD(wParam)) {
            case 1: // Local Play
                if (ModifyHostsFile(true)) {
                    FlushDNS();
                    app->isLocalPlay = true;
                    app->UpdateStatusText(L"Local Play Activated");
                }
                break;
            case 2: // Online Play
                if (ModifyHostsFile(false)) {
                    FlushDNS();
                    app->isLocalPlay = false;
                    app->UpdateStatusText(L"Online Play Restored");
                }
                break;
            }
            break;
        }
        case WM_DESTROY:
            DeleteObject(app->hTitleFont);
            PostQuitMessage(0);
            return 0;
        case WM_PAINT: {
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);

            // Set up custom paint for the window's status text
            HDC hdc = ps.hdc;
            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, app->isLocalPlay ? RGB(0, 200, 83) : RGB(229, 57, 53));
            SelectObject(hdc, app->hTitleFont);
            RECT rect = { 50, 140, 350, 180 };
            DrawText(hdc, app->currentStatus.c_str(), -1, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

            EndPaint(hwnd, &ps);
            return 0;
        }
        }
        return DefWindowProc(hwnd, uMsg, wParam, lParam);
    }

    void ApplyDarkMode() {
        SetClassLongPtr(mainWindow, GCLP_HBRBACKGROUND, (LONG_PTR)CreateSolidBrush(RGB(30, 30, 30))); // dark background
        SetClassLongPtr(mainWindow, GCLP_HICON, (LONG_PTR)LoadIcon(NULL, IDI_APPLICATION)); // set default icon
    }

    void CreateUI() {
        localPlayButton = CreateWindowEx(
            0, L"BUTTON", L"Local Play",
            WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON | BS_FLAT,
            50, 80, 140, 40,
            mainWindow, (HMENU)1, GetModuleHandle(NULL), NULL
        );

        onlinePlayButton = CreateWindowEx(
            0, L"BUTTON", L"Online Play",
            WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON | BS_FLAT,
            210, 80, 140, 40,
            mainWindow, (HMENU)2, GetModuleHandle(NULL), NULL
        );
    }

public:
    ModernHostSwitcher() {
        CreateCustomFonts();

        WNDCLASSEX wc = { sizeof(WNDCLASSEX) };
        wc.style = CS_HREDRAW | CS_VREDRAW;
        wc.lpfnWndProc = WindowProc;
        wc.hInstance = GetModuleHandle(NULL);
        wc.lpszClassName = L"ModernHostSwitcher";
        wc.hbrBackground = CreateSolidBrush(RGB(30, 30, 30)); // dark background
        RegisterClassEx(&wc);

        mainWindow = CreateWindowEx(
            WS_EX_APPWINDOW, L"ModernHostSwitcher", L"0x44Nine Switcher",
            WS_OVERLAPPEDWINDOW & ~WS_MAXIMIZEBOX & ~WS_SIZEBOX,
            CW_USEDEFAULT, CW_USEDEFAULT, 400, 250,
            NULL, NULL, GetModuleHandle(NULL), this
        );

        ApplyDarkMode();
        CreateUI();
    }

    void Show(int nCmdShow) {
        ShowWindow(mainWindow, nCmdShow);
        UpdateWindow(mainWindow);
    }

    int Run() {
        MSG msg = {};
        while (GetMessage(&msg, NULL, 0, 0)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        return (int)msg.wParam;
    }
};

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int nCmdShow) {
    ModernHostSwitcher app;
    app.Show(nCmdShow);
    return app.Run();
}
