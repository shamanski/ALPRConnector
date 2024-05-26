using System;
using System.Text.Json;

namespace ConsoleApp1
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
            return JsonSerializer.Deserialize<AppSettings>(json);
        }

        public static void SaveSettings(this AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText("appssetings.json", json);
        }
    }
}
