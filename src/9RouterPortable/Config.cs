using System;
using System.IO;
using System.Text.Json;

namespace NineRouterPortable
{
    public class LauncherConfig
    {
        public int Port { get; set; } = 20128;
        public string Host { get; set; } = "0.0.0.0";
        public bool AutoStart { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool OpenBrowserWhenStarted { get; set; } = false;
        public string InitialPassword { get; set; } = "123456";

        public static string GetConfigPath()
        {
            string baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "config", "launcher.json");
        }

        public static LauncherConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<LauncherConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading launcher config: " + ex.Message);
            }
            return new LauncherConfig();
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving launcher config: " + ex.Message);
            }
        }
    }
}
