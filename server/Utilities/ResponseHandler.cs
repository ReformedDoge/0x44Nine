using Newtonsoft.Json;
using System.Net;
using System.Text;

public static class ResponseHandler
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void SendSuccessResponse(HttpListenerResponse response, string encryptedData)
    {
        byte[] buffer = Utf8NoBom.GetBytes(encryptedData);
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    public static void SendErrorResponse(HttpListenerResponse response, string errorMessage)
    {
        var errorResponse = JsonConvert.SerializeObject(new { error = errorMessage });
        byte[] buffer = Utf8NoBom.GetBytes(errorResponse);
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }
}
