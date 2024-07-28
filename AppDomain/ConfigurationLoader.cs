using System.Text.Json;

namespace AppDomain
{
    public static class ConfigurationLoader
    {
        private static  AppSettings _settings;
        
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
            _settings = loaded;
            return loaded;
        }

        public static List<T> LoadSettings<T>() where T: Model
        {
            AppSettings options;
            if (!File.Exists("appsettings.json"))
            {
                options = new AppSettings();
                ConfigurationLoader.SaveSettings(options);
            }
            else
            {
                var json = File.ReadAllText("appsettings.json");
                options = JsonSerializer.Deserialize<AppSettings>(json);
                foreach (var reader in options.LprReaders)
                {
                    reader.Camera = options.Cameras.Where(x => x.Name == reader.CameraName).FirstOrDefault();
                    reader.ComPortPair = options.ComPortPairs.Where(x => x.Name == reader.ComPortPairName).FirstOrDefault();
                }
            }


            return typeof(T) switch
            {
                var t when t == typeof(Camera) => options.Cameras.Cast<T>().ToList(),
                var t when t == typeof(ComPortPair) => options.ComPortPairs.Cast<T>().ToList(),
                var t when t == typeof(LprReader) => options.LprReaders.Cast<T>().ToList(),
                _ => throw new NotImplementedException($"The type {typeof(T).Name} is not supported.")
            };

        }

        public static void SaveSettings(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText("appsettings.json", json);
        }

        public static void SaveSettings()
        {
            var settings = LoadSettings();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText("appsettings.json", json);
        }
    }
}
