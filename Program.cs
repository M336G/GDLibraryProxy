using System.Net;
using System.Text;

namespace GDLibraryProxy {
    class Program {
        // Domain for Geometry Dash Music and SFX Libraries
        public static string url = "geometrydashfiles.b-cdn.net";

        // All endpoints
        public static string[] pages = {
            "/music/musiclibrary_version.txt",
            "/music/musiclibrary.dat",
            "/music/musiclibrary_version_02.txt",
            "/music/musiclibrary_02.dat",
            "/sfx/sfxlibrary_version.txt",
            "/sfx/sfxlibrary.dat"
        };
        static async Task Main() {
            HttpListener server = new HttpListener();
            
            server.Prefixes.Add("http://127.0.0.1:8080/"); // Here, "8080" is the port, you can change it if you need

            server.Start();
            Console.WriteLine("Server Started");

            while(true) {
                HttpListenerContext context = server.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                byte[] buffer = Encoding.UTF8.GetBytes(await PageBehavior(context, url, pages));

                response.ContentLength64 = buffer.Length;

                Stream stream = response.OutputStream;

                stream.Write(buffer, 0, buffer.Length);

                context.Response.Close();

                Console.WriteLine($"[{response.StatusCode}] {request.HttpMethod} \"{request.Url.AbsolutePath}\"");
            }
        }
        static async Task<string> PageBehavior(HttpListenerContext context, string url, string[] pages) {
            string response = "Not Found";
            context.Response.StatusCode = 404;

            HttpClient client = new HttpClient();
    
            foreach (string page in pages) {
                if (context.Request.Url.AbsolutePath == page) {
                    try {
                        response = await client.GetStringAsync($"https://{url}{page}");
                        context.Response.StatusCode = 200;
                    } catch (HttpRequestException) {
                        context.Response.StatusCode = 500;
                        response = "Internal Server Error";
                    }
                    return response;
                }
            }
            return response;
        }
    }
}