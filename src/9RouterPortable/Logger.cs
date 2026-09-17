using System;
using System.IO;

namespace NineRouterPortable
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;

        private static string GetLogFilePath()
        {
            if (_logFilePath != null) return _logFilePath;
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string logsDir = Path.Combine(baseDir, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                _logFilePath = Path.Combine(logsDir, "launcher.log");
            }
            catch
            {
                _logFilePath = Path.Combine(Path.GetTempPath(), "9router-launcher.log");
            }
            return _logFilePath;
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    string path = GetLogFilePath();
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }
}
