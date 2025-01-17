using Ninelives_Offline.Configuration;
using Ninelives_Offline.Controllers;
using Ninelives_Offline.Services;
using Ninelives_Offline.Utilities;
using System.Net;

namespace Ninelives_Offline
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome to the 0x44Nine Offline Server Emulator!");
            Console.WriteLine("Test Version - Contact: 0x44oge on Discord!");
            AppConfig.LoadOrInitializeConfig();

            Console.WriteLine($"Database File: {AppConfig.DbFile}");

            // Use dependency injection pattern to share single instances
            var services = InitializeServices();
            var requestHandler = new RequestHandler(services.Item1, services.Item2);

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/kyrill/");
            listener.Start();
            Console.WriteLine("Server is listening on http://localhost:8080/kyrill/");

            await ListenForRequestsAsync(listener, requestHandler);
        }

        private static (RequestProcessorService, AccountController) InitializeServices()
        {
            var cryptoService = new CryptographyService();
            var dbService = new DatabaseService();
            dbService.EnsureDatabase();

            var authService = new AuthenticationService(cryptoService);
            var sessionService = new SessionService();
            var accountController = new AccountController(authService, cryptoService, dbService, sessionService);
            var requestProcessorService = new RequestProcessorService(cryptoService);

            return (requestProcessorService, accountController);
        }

        static async Task ListenForRequestsAsync(HttpListener listener, RequestHandler requestHandler)
        {
            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    // Handle each request in a separate task to prevent memory buildup
                    _ = Task.Run(() => requestHandler.HandleRequest(context))
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                Console.WriteLine($"Error: {t.Exception?.InnerException?.Message}");
                                Console.WriteLine($"Stack Trace: {t.Exception?.InnerException?.StackTrace}");
                            }
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Critical Error: {ex.Message}");
                }
            }
        }
    }
}