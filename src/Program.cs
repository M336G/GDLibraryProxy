using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;

namespace GDLibraryProxy {
    class Program {
        private static bool serverStarted = false; // If this is set to false during the server's execution, it will stop

        private static readonly HttpListener httpServer = new HttpListener();
        public static readonly HttpClient httpClient = new HttpClient();

        private static string SERVER_BASE_URL = "http://localhost";
        private static int SERVER_PORT = 8080;
        public static bool LOG_REQUESTS = true;

        private static bool ENABLE_CACHE = true;
        private static int CACHE_MAX_HOURS = 24;

        public static string GD_LIBRARY_BASE_URL = "https://geometrydashfiles.b-cdn.net";

        private static IMemoryCache CACHE = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 * 1024 * 25, CompactionPercentage = 0.7  }); // Limit the cache to a maximum of 25MB

        private static readonly string[] cachedEndpoints = [
            "/music/musiclibrary_version.txt",
            "/music/musiclibrary.dat",
            "/music/musiclibrary_version_02.txt",
            "/music/musiclibrary_02.dat",
            "/sfx/sfxlibrary_version.txt",
            "/sfx/sfxlibrary.dat"
        ];

        private static async Task HandleRequest(HttpListenerContext context) {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string urlEndpoint = request.Url?.AbsolutePath ?? string.Empty;

            string? rawResponse;
            string? cachedResponse;

            try {
                if (urlEndpoint.StartsWith("/music/") || urlEndpoint.StartsWith("/sfx/")) { // Don't bother with handling if none of the paths are for the music/sfx libraries
                    if (cachedEndpoints.Contains(urlEndpoint)) { // Check if cached
                        if (CACHE.TryGetValue(urlEndpoint, out cachedResponse)) {
                            rawResponse = cachedResponse;
                        } else { // If not, fetch (and store it if cache is enabled)
                            rawResponse = await Utils.GetFromLibraryEndpoint(urlEndpoint);
                            if (ENABLE_CACHE) CACHE.Set(urlEndpoint, rawResponse, new MemoryCacheEntryOptions {
                                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(CACHE_MAX_HOURS),
                                Size = rawResponse?.Length ?? 0
                            });
                        }
                    } else { // If it's not cachable (songs), simply download and return the file
                        rawResponse = await Utils.GetFromLibraryEndpoint(urlEndpoint);
                    }

                    if (!string.IsNullOrEmpty(rawResponse)) { // Make sure it's not empty
                        context.Response.StatusCode = 200;
                    } else {
                        context.Response.StatusCode = 500;
                        rawResponse = "Internal Server Error";
                    }
                } else {
                    context.Response.StatusCode = 404;
                    rawResponse = "Not Found";
                }

                Utils.LogRequest(request, context.Response.StatusCode, urlEndpoint);
                await Utils.WriteResponse(response, rawResponse);
            } catch (Exception e) {
                Console.Error.WriteLine($"An unexpected error occured during request handling: {e.Message} ({e.StackTrace})");

                context.Response.StatusCode = 500;
                Utils.LogRequest(request, 500, urlEndpoint);
                await Utils.WriteResponse(response, "Internal Server Error");
            }
        }

        private static async Task Main() {
            await EnvReader.Load(".env"); // Load environment variables from a .env file

            // Try to parse every environment variable
            string? envServerBaseURL = Environment.GetEnvironmentVariable("SERVER_PORT");
            if (envServerBaseURL != null && Uri.IsWellFormedUriString(envServerBaseURL, UriKind.Absolute)) SERVER_BASE_URL = envServerBaseURL;
            string? envServerPort = Environment.GetEnvironmentVariable("SERVER_PORT");
            if (envServerPort != null && int.TryParse(envServerPort, out _)) SERVER_PORT = int.Parse(envServerPort);
            string? envGDLibraryBaseURL = Environment.GetEnvironmentVariable("GD_LIBRARY_BASE_URL");
            if (envGDLibraryBaseURL != null && Uri.IsWellFormedUriString(envGDLibraryBaseURL, UriKind.Absolute)) GD_LIBRARY_BASE_URL = envGDLibraryBaseURL;
            string? envLogRequests = Environment.GetEnvironmentVariable("LOG_REQUESTS");
            if (envLogRequests != null && bool.TryParse(envLogRequests, out _)) LOG_REQUESTS = bool.Parse(envLogRequests);
            string? envEnableCache = Environment.GetEnvironmentVariable("ENABLE_CACHE");
            if (envEnableCache != null && bool.TryParse(envEnableCache, out _)) ENABLE_CACHE = bool.Parse(envEnableCache);
            string? envCacheMaxHours = Environment.GetEnvironmentVariable("CACHE_MAX_HOURS");
            if (envCacheMaxHours != null && int.TryParse(envCacheMaxHours, out _)) CACHE_MAX_HOURS = int.Parse(envCacheMaxHours);


            httpServer.Prefixes.Add($@"{SERVER_BASE_URL}:{SERVER_PORT}/");
            httpServer.Start();
            serverStarted = true;

            Console.WriteLine($@"Server started on {SERVER_BASE_URL}:{SERVER_PORT}/");

            // Create workers depending on the amount of threads available
            var channel = Channel.CreateUnbounded<HttpListenerContext>();
            int numWorkers = Environment.ProcessorCount;
            for (int i = 0; i < numWorkers; i++) {
                _ = Task.Run(async () => {
                    while (await channel.Reader.WaitToReadAsync()) {
                        var context = await channel.Reader.ReadAsync();
                        try {
                            await HandleRequest(context);
                        } catch (Exception e) {
                            Console.Error.WriteLine($"An unexpected error occured during request handling: {e.Message} ({e.StackTrace})");
                        }
                    }
                });
            }

            while (serverStarted) {
                try {
                    var context = await httpServer.GetContextAsync();
                    await channel.Writer.WriteAsync(context);
                } catch (Exception e) {
                    Console.Error.WriteLine($@"An unexpected error occured during the program's execution: {e.Message} ({e.StackTrace})");
                    serverStarted = false;
                }
            }

            httpServer.Stop();
            channel.Writer.Complete();
            await channel.Reader.Completion;
            CACHE.Dispose();
        }
    }
}