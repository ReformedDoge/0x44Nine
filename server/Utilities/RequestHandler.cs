using Ninelives_Offline.Configuration;
using Ninelives_Offline.Controllers;
using System.Net;

namespace Ninelives_Offline.Utilities
{
    public class RequestHandler
    {
        private readonly RequestProcessorService _processorService;
        private readonly AccountController _accountController;
        private readonly IReadOnlyDictionary<string, Action<string, string, HttpListenerContext>> _routes;

        public RequestHandler(RequestProcessorService processorService, AccountController accountController)
        {
            _processorService = processorService;
            _accountController = accountController;

            // Use IReadOnlyDictionary to prevent unnecessary memory allocations
            _routes = new Dictionary<string, Action<string, string, HttpListenerContext>>
            {
                ["/kyrill/addAccount"] = _accountController.ProcessAddAccount,
                ["/kyrill/login"] = _accountController.ProcessLogin,
                ["/kyrill/verifyAccount"] = _accountController.ProcessVerifyAccount,
                ["/kyrill/characterList"] = _accountController.ProcessCharacterList,
                ["/kyrill/connectionKeep"] = _accountController.ProcessConnectionKeep,
                ["/kyrill/newCharacter"] = _accountController.ProcessCreateCharacter,
                ["/kyrill/saveCharacterItems"] = _accountController.ProcessSaveCharacterItems,
                ["/kyrill/saveCharacter"] = _accountController.ProcessSaveCharacter,
                ["/kyrill/removeCharacter"] = _accountController.ProcessRemoveCharacter,
                ["/kyrill/characterConnectionKeep"] = _accountController.ProcessCharacterConnectionKeep,
                ["/kyrill/loadCharacter"] = _accountController.ProcessLoadCharacter,
                ["/kyrill/loadCharacterItems"] = _accountController.ProcessLoadCharacterItems,
                ["/kyrill/saveCharacterItemsDiff"] = _accountController.ProcessSaveCharacterItemsDiff,
                ["/kyrill/saveCharacterPortrait"] = _accountController.ProcessSaveCharacterPortrait,
                ["/kyrill/loadCharacterQuests"] = _accountController.ProcessLoadCharacterQuests,
                ["/kyrill/completeCharacterQuest"] = _accountController.ProcessCompleteCharacterQuest,
                ["/kyrill/removeCharacterQuest"] = _accountController.ProcessRemoveCharacterQuest,
                ["/kyrill/saveCharacterQuest"] = _accountController.ProcessSaveCharacterQuest,
                ["/kyrill/loadAlchemyRecipeList"] = _accountController.ProcessLoadAlchemyRecipeList,
                ["/kyrill/loadAlchemyRecipeCode"] = _accountController.ProcessLoadAlchemyRecipeCode,
                ["/kyrill/loadAlchemyMixedItems"] = _accountController.ProcessloadAlchemyMixedItems,
                ["/kyrill/saveAlchemyMixedItem"] = _accountController.ProcessSaveAlchemyMixedItem,
                ["/kyrill/bagUnlockBySG"] = _accountController.ProcessBagUnlockBySG,
                ["/kyrill/bankUnlockBySG"] = _accountController.ProcessBankUnlockBySG,
                ["/kyrill/addSharedBank"] = _accountController.ProcessAddSharedBank,
                ["/kyrill/rmResetSkilltree"] = _accountController.ProcessRMResetSkilltree,
                ["/kyrill/addCharacterLimit"] = _accountController.ProcessAddCharacterLimit,
                ["/kyrill/loadInboxItems"] = _accountController.ProcessLoadInboxItems,
                ["/kyrill/bankUnlockByItem"] = _accountController.ProcessBankUnlockByItem,
                ["/kyrill/saveCharacterBagBankCount"] = _accountController.ProcessSaveCharacterBagBankCount,
                ["/kyrill/boxRequest"] = _accountController.ProcessBoxRequest,
                ["/kyrill/itemDropRequest"] = _accountController.ProcessItemDropRequest,
                ["/kyrill/shopItemRequest"] = _accountController.ProcessShopItemRequest,
            };
        }

        public async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            Console.WriteLine($"Received {request.HttpMethod} request: {request.Url.AbsolutePath}");
            string encryptedData;

            // Use StreamReader.ReadToEndAsync for better memory handling
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
            {
                encryptedData = await reader.ReadToEndAsync();
            }

            try
            {
                string decryptedData = _processorService.ProcessRequest(encryptedData);
                Console.WriteLine("Decrypted Data:");
                Console.WriteLine(decryptedData);

                if (_routes.TryGetValue(request.Url.AbsolutePath, out var handler))
                {
                    string sessionKey = AppConfig.CommonKey;
                    handler(decryptedData, sessionKey, context);
                }
                else
                {
                    ResponseHandler.SendErrorResponse(context.Response, "Invalid endpoint.");
                }
            }
            catch (Exception ex)
            {
                ResponseHandler.SendErrorResponse(context.Response, $"Request processing error: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}