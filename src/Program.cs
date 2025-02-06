using System.Net;
using System.Text;

namespace GDLibraryProxy {
    class Program {
        private static bool serverStarted = false;

        private static readonly HttpListener httpServer = new HttpListener();
        private static readonly HttpClient httpClient = new HttpClient();

        private static int SERVER_PORT = 8080;
        private static bool LOG_REQUESTS = true;

        private static string GD_LIBRARY_FQDN = "geometrydashfiles.b-cdn.net";

        private static async Task<string?> downloadFromURL(string url) {
            try {
                return await httpClient.GetStringAsync(url);
            } catch (HttpRequestException error) {
                Console.Error.WriteLine($@"Could not fetch from the following URL ({error.StatusCode}):", url);
                return null;
            }
        }

        private static readonly Dictionary<string, Func<Task<string>>> gdLibraryHandlers = new() {
            ["/music/musiclibrary_version.txt"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/music/musiclibrary_version.txt"),
            ["/music/musiclibrary.dat"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/music/musiclibrary.dat"),
            ["/music/musiclibrary_version_02.txt"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/music/musiclibrary_version_02.txt"),
            ["/music/musiclibrary_02.dat"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/music/musiclibrary_02.dat"),
            ["/sfx/sfxlibrary_version.txt"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/sfx/sfxlibrary_version.txt"),
            ["/sfx/sfxlibrary.dat"] = async () => await downloadFromURL($@"https://{GD_LIBRARY_FQDN}/sfx/sfxlibrary.dat")
        };

        private static async Task Main() {
            await EnvReader.Load(".env");

            string envPort = Environment.GetEnvironmentVariable("SERVER_PORT");
            if (envPort != null && int.TryParse(envPort, out _)) SERVER_PORT = int.Parse(envPort);
            string envGDLibraryFQDN = Environment.GetEnvironmentVariable("GD_LIBRARY_FQDN");
            if (envGDLibraryFQDN != null && Uri.CheckHostName(envGDLibraryFQDN).Equals(UriHostNameType.Dns)) GD_LIBRARY_FQDN = envGDLibraryFQDN;
            string envLogRequests = Environment.GetEnvironmentVariable("LOG_REQUESTS");
            if (envLogRequests != null && bool.TryParse(envLogRequests, out _)) LOG_REQUESTS = bool.Parse(envLogRequests);

            httpServer.Prefixes.Add($@"http://127.0.0.1:{SERVER_PORT}/");
            httpServer.Start();
            serverStarted = true;

            Console.WriteLine($@"Server started on http://127.0.0.1:{SERVER_PORT}");

            while(serverStarted) {
                try {
                    HttpListenerContext context = httpServer.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    string urlPath = request.Url.AbsolutePath;

                    string rawResponse;

                    if (gdLibraryHandlers.TryGetValue(urlPath, out Func<Task<string>> handler)) {
                        string handlerResponse = await handler();
                    
                        if (handlerResponse != null && handlerResponse != "") {
                            context.Response.StatusCode = 200;
                            rawResponse = handlerResponse;
                        } else {
                            context.Response.StatusCode = 500;
                            rawResponse = "Internal Server Error";
                        }
                    } else {
                        context.Response.StatusCode = 404;
                        rawResponse = "Not Found";
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(rawResponse);
                    response.ContentLength64 = buffer.Length;

                    Stream stream = response.OutputStream;
                    stream.Write(buffer, 0, buffer.Length);

                    if (LOG_REQUESTS) {
                        DateTime utcTime = DateTime.UtcNow;
                        Console.WriteLine($@"({utcTime} UTC) '{request.HttpMethod} {context.Response.StatusCode} {urlPath} HTTP/{request.ProtocolVersion}' (host: {request.UserHostName}, requester: {request.RemoteEndPoint}, user-agent: {request.UserAgent})");
                    }
                
                    context.Response.Close();
                } catch (Exception e) {
                    Console.Error.WriteLine("An unexpected error occured during the program's execution:", e);
                    serverStarted = false;
                }
            }
            httpServer.Stop();
        }
    }
}