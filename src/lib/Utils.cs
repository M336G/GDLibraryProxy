using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace GDLibraryProxy {
    class Utils {
        public static async Task WriteResponse(HttpListenerResponse response, string content) {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        public static void LogRequest(HttpListenerRequest request, int statusCode, string urlEndpoint) {
            if (Program.LOG_REQUESTS) {
                DateTime utcTime = DateTime.UtcNow;
                Console.WriteLine($@"({utcTime} UTC) '{request.HttpMethod} {statusCode} {urlEndpoint} HTTP/{request.ProtocolVersion}' (host: {request.UserHostName}, requester: {request.RemoteEndPoint}, user-agent: {request.UserAgent})");
            }
        }

        public static Task<string?> GenerateLibraryToken(string endpoint, long expires) {
            try {
                using (MD5 md5 = MD5.Create()) {
                    byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes($@"8501f9c2-75ba-4230-8188-51037c4da102{endpoint}{expires}"));

                    string base64Hash = Convert.ToBase64String(hashBytes)
                        .Replace("+", "-")
                        .Replace("/", "_")
                        .TrimEnd('=');

                    return Task.FromResult<string?>(base64Hash);
                }
            } catch (Exception error) {
                Console.Error.WriteLine($@"Could not generate library token: {error.Message} ({error.StackTrace})");
                return Task.FromResult<string?>(null);
            }
        }

        public static async Task<string?> GetFromLibraryEndpoint(string endpoint) {
            try {
                long unixTimestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                long expires = unixTimestamp + 3600; // 1 hour later

                return await Program.httpClient.GetStringAsync($"{Program.GD_LIBRARY_BASE_URL}{endpoint}?expires={expires}&token={await GenerateLibraryToken(endpoint, expires)}");
            } catch (HttpRequestException error) {
                Console.Error.WriteLine($@"Could not fetch from {endpoint} ({error.StatusCode}): {error.Message} ({error.StackTrace})");
                return null;
            }
        }
    }
}