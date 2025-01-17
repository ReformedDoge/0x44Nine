using Ninelives_Offline.Configuration;
using Ninelives_Offline.Services;
using System.Web;

public class RequestProcessorService
{
    private readonly CryptographyService _cryptoService;
    private const int MinRequestLength = 4;

    public RequestProcessorService(CryptographyService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public string ProcessRequest(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty");

        if (encryptedData.Length > MinRequestLength)
        {
            encryptedData = encryptedData[MinRequestLength..];
        }

        string urlDecodedData = HttpUtility.UrlDecode(encryptedData);

        if (urlDecodedData.Length < AppConfig.SessionKeyLength)
            throw new ArgumentException("Invalid request data length");

        ReadOnlySpan<char> sessionKey = urlDecodedData.AsSpan(0, AppConfig.SessionKeyLength);
        ReadOnlySpan<char> encryptedPart = urlDecodedData.AsSpan(AppConfig.SessionKeyLength);

        string iv = CryptographyService.MakeIV(sessionKey.ToString(), 16);
        return CryptographyService.DecryptAES256(AppConfig.CommonKey, iv, FixBase64Padding(encryptedPart.ToString()));
    }

    private static string FixBase64Padding(string base64)
    {
        int padding = base64.Length % 4;
        return padding > 0 ? base64 + new string('=', 4 - padding) : base64;
    }
}
