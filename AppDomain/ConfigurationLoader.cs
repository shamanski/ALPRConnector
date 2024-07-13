using System.Text.Json;

namespace AppDomain
{
    public static class ConfigurationLoader
    {
        public static AppSettings LoadSettings()
        {
            if (!File.Exists("appsettings.json"))
            {
                var options = new AppSettings();
                ConfigurationLoader.SaveSettings(options);
                return options;
            }

            var json = File.ReadAllText("appsettings.json");
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            foreach (var reader in loaded.LprReaders)
            {
                reader.Camera = loaded.Cameras.Where(x => x.Name == reader.CameraName).FirstOrDefault();
                reader.ComPortPair = loaded.ComPortPairs.Where(x => x.Name == reader.ComPortPairName).FirstOrDefault();
            }
            return loaded;
        }

        public static void SaveSettings(this AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText("appsettings.json", json);
        }
    }
}
