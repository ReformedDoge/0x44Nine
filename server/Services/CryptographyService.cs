using System.Security.Cryptography;
using System.Text;

namespace Ninelives_Offline.Services
{
    public class CryptographyService
    {
        private static readonly Random random = new Random();

        public static string MakeRandomText(int byteCount)
        {
            byte[] byteArray = new byte[byteCount];
            random.NextBytes(byteArray);

            StringBuilder hexString = new StringBuilder(byteCount * 2);
            foreach (byte b in byteArray)
            {
                hexString.AppendFormat("{0:x2}", b);
            }
            return hexString.ToString();
        }

        public static string MakeIV(string code, int length)
        {
            int codeLength = code.Length;
            if (codeLength > length)
            {
                int num = (codeLength - length) / 2;
                return code.Substring(num, length);
            }
            return code;
        }

        // Add encryption/decryption methods here
        public static string DecryptAES256(string key_, string iv_, string decText_)
        {
#pragma warning disable SYSLIB0022
            RijndaelManaged rijndaelManaged = new RijndaelManaged();
#pragma warning restore SYSLIB0022
            rijndaelManaged.Padding = PaddingMode.None;
            rijndaelManaged.Mode = CipherMode.CBC;
            rijndaelManaged.KeySize = 256;
            rijndaelManaged.BlockSize = 128;

            byte[] bytes = Encoding.UTF8.GetBytes(key_);
            byte[] bytes2 = Encoding.UTF8.GetBytes(iv_);
            ICryptoTransform cryptoTransform = rijndaelManaged.CreateDecryptor(bytes, bytes2);

            // Fix Base64 padding if necessary
            string fixedBase64 = FixBase64Padding(decText_);
            byte[] array = Convert.FromBase64String(fixedBase64);
            byte[] array2 = new byte[array.Length];

            using (MemoryStream memoryStream = new MemoryStream(array))
            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Read))
            {
                cryptoStream.Read(array2, 0, array2.Length);
            }

            string decryptedString = Encoding.UTF8.GetString(array2);

            int paddingLength = array2[array2.Length - 1];
            decryptedString = decryptedString.Substring(0, decryptedString.Length - paddingLength);

            return decryptedString;
        }

        public static string EncryptAES256(string key_, string iv_, string encText_)
        {
            // Generate a random string (IV) using the same method as the client
            string randomIV = MakeRandomText(20);  // This is similar to the client's random text generation
            string iv16Bytes = MakeIV(randomIV, 16);

            byte[] ivBytes = Encoding.UTF8.GetBytes(iv16Bytes);

            // AES encryption setup
#pragma warning disable SYSLIB0022
            RijndaelManaged rijndaelManaged = new RijndaelManaged();
#pragma warning restore SYSLIB0022
            rijndaelManaged.Padding = PaddingMode.PKCS7;
            rijndaelManaged.Mode = CipherMode.CBC;
            rijndaelManaged.KeySize = 256;
            rijndaelManaged.BlockSize = 128;

            // Convert key to byte array
            byte[] keyBytes = Encoding.UTF8.GetBytes(key_);

            // Create encryptor
            ICryptoTransform cryptoTransform = rijndaelManaged.CreateEncryptor(keyBytes, ivBytes);

            // Convert text to encrypt into byte array
            byte[] encTextBytes = Encoding.UTF8.GetBytes(encText_);

            // Perform encryption
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write);
            cryptoStream.Write(encTextBytes, 0, encTextBytes.Length);
            cryptoStream.FlushFinalBlock();

            byte[] encryptedBytes = memoryStream.ToArray();
            string encryptedText = Convert.ToBase64String(encryptedBytes);  // Return encrypted text as Base64 string

            string finalResult = randomIV + encryptedText;
            return finalResult;
        }

        public static string FixBase64Padding(string base64)
        {
            int padding = base64.Length % 4;
            if (padding > 0)
            {
                base64 += new string('=', 4 - padding);
            }
            return base64;
        }
    }
}
