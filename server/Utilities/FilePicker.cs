using System.Runtime.InteropServices;

namespace Ninelives_Offline.Configuration
{
    public static class FilePicker
    {
        // P/Invoke to call the native Windows Save File Dialog
        [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

        public static string ShowSaveFileDialog()
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFile = new string(new char[256]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFilter = "Database Files (*.db)\0*.db\0All Files (*.*)\0*.*\0";
            ofn.lpstrTitle = "Select Database File Location";
            ofn.lpstrFileTitle = "account.db";
            ofn.Flags = 0x00000002 | 0x00080000; // OFN_PATHMUSTEXIST | OFN_OVERWRITEPROMPT

            if (GetSaveFileName(ofn))
            {
                return ofn.lpstrFile;
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
        }
    }
}
