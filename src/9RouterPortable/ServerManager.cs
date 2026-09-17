using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NineRouterPortable
{
    public enum ServerState
    {
        Stopped, Starting, Running, Stopping, Failed, Crashed
    }

    public class ServerManager
    {
        private Process? _nodeProcess;
        private ServerState _state = ServerState.Stopped;
        private readonly object _stateLock = new object();
        private CancellationTokenSource? _healthCheckCts;

        public event Action<ServerState, string>? StateChanged;
        public event Action<string>? LogOutput;

        public ServerState State
        {
            get { lock (_stateLock) { return _state; } }
            private set { lock (_stateLock) { _state = value; } StateChanged?.Invoke(value, GetStatusDescription(value)); }
        }

        public int CurrentPid => _nodeProcess != null && !_nodeProcess.HasExited ? _nodeProcess.Id : 0;

        private string GetStatusDescription(ServerState s) => s.ToString().ToUpper();

        public void EnsureSecretsAndStructure(int port)
        {
            string baseDir = AppContext.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(Path.Combine(dataDir, "secrets"));
            Directory.CreateDirectory(Path.Combine(dataDir, "config"));
            Directory.CreateDirectory(Path.Combine(dataDir, "db"));
            Directory.CreateDirectory(Path.Combine(baseDir, "logs"));
            Directory.CreateDirectory(Path.Combine(dataDir, "logs"));

            GenerateSecret(Path.Combine(dataDir, "secrets", "jwt.secret"), 32);
            GenerateSecret(Path.Combine(dataDir, "secrets", "api_key.secret"), 32);
        }

        private void GenerateSecret(string filePath, int byteLength)
        {
            if (!File.Exists(filePath))
            {
                byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
                File.WriteAllText(filePath, Convert.ToHexString(bytes).ToLowerInvariant(), Encoding.UTF8);
            }
        }

        public bool IsPortInUse(int port)
        {
            try
            {
                var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                foreach (var l in props.GetActiveTcpListeners()) if (l.Port == port) return true;
                foreach (var c in props.GetActiveTcpConnections()) if (c.LocalEndPoint.Port == port) return true;
            }
            catch { }
            return false;
        }

        public void Start(int port)
        {
            if (State == ServerState.Starting || State == ServerState.Running) return;
            State = ServerState.Starting;
            Task.Run(() => PerformStart(port));
        }

        private void PerformStart(int port)
        {
            try
            {
                EnsureSecretsAndStructure(port);
                if (IsPortInUse(port)) { State = ServerState.Failed; return; }

                string baseDir = AppContext.BaseDirectory;
                string nodeExe = Path.Combine(baseDir, "runtime", "node", "node.exe");
                string appDir = Path.Combine(baseDir, "app", "9router");
                string entry = Path.Combine(appDir, "server.js");

                var psi = new ProcessStartInfo
                {
                    FileName = nodeExe,
                    Arguments = $"\"{entry}\"",
                    WorkingDirectory = appDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.Environment["PORT"] = port.ToString();
                psi.Environment["HOSTNAME"] = "0.0.0.0";
                psi.Environment["DATA_DIR"] = Path.Combine(baseDir, "data");

                var config = LauncherConfig.Load();
                if (!string.IsNullOrEmpty(config.InitialPassword))
                {
                    psi.Environment["INITIAL_PASSWORD"] = config.InitialPassword;
                }
                _nodeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _nodeProcess.Exited += (s, e) => { if (State == ServerState.Running) State = ServerState.Crashed; else State = ServerState.Stopped; };
                _nodeProcess.Start();
                JobTracker.AddProcess(_nodeProcess.Handle);

                if (WaitForHealthCheck(port, 30500)) State = ServerState.Running;
                else { Stop(); State = ServerState.Failed; }
            }
            catch { State = ServerState.Failed; }
        }

        private bool WaitForHealthCheck(int port, int timeout)
        {
            _healthCheckCts = new CancellationTokenSource();
            int elapsed = 0;
            Thread.Sleep(500);
            while (elapsed < timeout)
            {
                if (_nodeProcess == null || _nodeProcess.HasExited) return false;
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                    if (client.GetAsync($"http://127.0.0.1:{port}/").Result.IsSuccessStatusCode) return true;
                }
                catch { }
                Thread.Sleep(500); elapsed += 500;
            }
            return false;
        }

        public void Stop()
        {
            State = ServerState.Stopping;
            _healthCheckCts?.Cancel();
            Task.Run(() => {
                if (_nodeProcess != null && !_nodeProcess.HasExited) _nodeProcess.Kill(true);
                _nodeProcess = null;
                State = ServerState.Stopped;
            });
        }
    }
}
