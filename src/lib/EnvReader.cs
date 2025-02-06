namespace GDLibraryProxy {
    // https://rmauro.dev/read-env-file-in-csharp/
    class EnvReader {
        public static async Task Load(string filePath) {
            if (File.Exists(filePath)) {
                foreach (string line in File.ReadAllLines(filePath)) {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue; // Skip empty lines and comments

                    string[] parts = line.Split("=", 2);
                    if (parts.Length != 2)
                        continue; // Skip lines that are not key-value pairs

                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}