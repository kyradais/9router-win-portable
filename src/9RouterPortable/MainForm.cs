using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace NineRouterPortable
{
    public partial class MainForm : Form
    {
        private readonly ServerManager _serverManager;
        private readonly LauncherConfig _config;
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;

        private Label? _lblStatus;
        private Label? _lblPid;
        private Button? _btnToggle;
        private LinkLabel? _lnkLocal;
        private LinkLabel? _lnkLan;
        private TextBox? _txtLog;
        private CheckBox? _chkMinimizeToTray;
        private bool _isQuitting = false;

        public MainForm()
        {
            _serverManager = new ServerManager();
            _config = LauncherConfig.Load();

            InitializeComponentCustom();

            _serverManager.StateChanged += OnServerStateChanged;
            _serverManager.LogOutput += OnServerLogOutput;

            JobTracker.Initialize();

        }

        private void InitializeComponentCustom()
        {
            this.Text = "9Router Portable";
            this.Size = new Size(460, 420);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            try { this.Icon = SystemIcons.Application; } catch { }

            int margin = 15;
            int y = 15;

            var lblTitle = new Label { Text = "9Router Portable", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(margin, y), AutoSize = true };
            this.Controls.Add(lblTitle);
            y += 30;

            var panelStatus = new Panel { Location = new Point(margin, y), Size = new Size(415, 45), BackColor = Color.FromArgb(245, 245, 245) };
            _lblStatus = new Label { Text = "● STOPPED", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.DarkRed, Location = new Point(12, 12), AutoSize = true };
            panelStatus.Controls.Add(_lblStatus);
            _lblPid = new Label { Text = "", Font = new Font("Segoe UI", 9, FontStyle.Regular), ForeColor = Color.Gray, Location = new Point(300, 14), AutoSize = true };
            panelStatus.Controls.Add(_lblPid);
            this.Controls.Add(panelStatus);
            y += 55;

            _btnToggle = new Button { Text = "START SERVER", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(margin, y), Size = new Size(415, 35), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _btnToggle.Click += BtnToggle_Click;
            this.Controls.Add(_btnToggle);
            y += 45;

            var lblUrls = new Label { Text = "Access URLs:", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(margin, y), AutoSize = true };
            this.Controls.Add(lblUrls);
            y += 20;

            _lnkLocal = new LinkLabel { Text = $"Local: http://127.0.0.1:{_config.Port}", Location = new Point(margin, y), AutoSize = true };
            _lnkLocal.LinkClicked += (s, e) => OpenUrl($"http://127.0.0.1:{_config.Port}");
            this.Controls.Add(_lnkLocal);
            y += 20;

            var ips = NetworkUtils.GetLocalIpAddresses();
            string lanIp = ips.Count > 1 ? ips[1] : "192.168.1.20";
            _lnkLan = new LinkLabel { Text = $"LAN:   http://{lanIp}:{_config.Port}", Location = new Point(margin, y), AutoSize = true };
            _lnkLan.LinkClicked += (s, e) => OpenUrl($"http://{lanIp}:{_config.Port}");
            this.Controls.Add(_lnkLan);
            y += 25;

            var lblLogTitle = new Label { Text = "Activity Log:", Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(margin, y), AutoSize = true };
            this.Controls.Add(lblLogTitle);
            y += 18;

            _txtLog = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(margin, y), Size = new Size(415, 75), Font = new Font("Consolas", 8.5f), BackColor = Color.White };
            this.Controls.Add(_txtLog);
            y += 85;

            _chkMinimizeToTray = new CheckBox { Text = "Minimize to system tray on close", Location = new Point(margin, y), AutoSize = true };
            _chkMinimizeToTray.CheckedChanged += (s, e) => { _config.MinimizeToTray = _chkMinimizeToTray.Checked; _config.Save(); };
            this.Controls.Add(_chkMinimizeToTray);

            SetupTray();

            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;

            if (_chkMinimizeToTray != null)
            {
                _chkMinimizeToTray.Checked = _config.MinimizeToTray;
            }

            if (_config.AutoStart)
            {
                this.Shown += (s, e) => { _serverManager.Start(_config.Port); };
            }
        }
        private void SetupTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Show Control Panel", null, (s, e) => ShowWindow());
            _trayMenu.Items.Add("Start Server", null, (s, e) => _serverManager.Start(_config.Port));
            _trayMenu.Items.Add("Stop Server", null, (s, e) => _serverManager.Stop());
            _trayMenu.Items.Add("Open Dashboard", null, (s, e) => OpenUrl($"http://127.0.0.1:{_config.Port}"));
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _trayIcon = new NotifyIcon { Text = "9Router Portable - Stopped", Icon = SystemIcons.Application, ContextMenuStrip = _trayMenu, Visible = true };
            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized && _config.MinimizeToTray)
            {
                this.Hide();
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isQuitting && _config.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }

            if (!_isQuitting)
            {
                e.Cancel = true;
                ExitApplication();
            }
        }

        private void ExitApplication()
        {
            if (_isQuitting) return;
            _isQuitting = true;

            if (_serverManager.State == ServerState.Running || _serverManager.State == ServerState.Starting)
            {
                _serverManager.Stop();
                System.Threading.Thread.Sleep(2000);
            }

            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            }
            catch { }

            Environment.Exit(0);
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            if (_serverManager.State == ServerState.Stopped || _serverManager.State == ServerState.Failed || _serverManager.State == ServerState.Crashed)
            {
                _serverManager.Start(_config.Port);
            }
            else if (_serverManager.State == ServerState.Running)
            {
                _serverManager.Stop();
            }
        }

        private void OnServerStateChanged(ServerState state, string desc)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnServerStateChanged(state, desc)));
                return;
            }

            if (_lblStatus != null)
            {
                _lblStatus.Text = "● " + desc;
                _lblStatus.ForeColor = state switch
                {
                    ServerState.Running => Color.DarkGreen,
                    ServerState.Starting => Color.DarkOrange,
                    ServerState.Stopping => Color.DarkBlue,
                    ServerState.Failed or ServerState.Crashed => Color.DarkRed,
                    _ => Color.Gray
                };
            }

            if (_lblPid != null)
            {
                int pid = _serverManager.CurrentPid;
                _lblPid.Text = pid > 0 ? $"PID: {pid}" : "";
            }

            if (_btnToggle != null)
            {
                if (state == ServerState.Running)
                {
                    _btnToggle.Text = "STOP SERVER";
                    _btnToggle.BackColor = Color.FromArgb(200, 50, 50);
                }
                else if (state == ServerState.Stopped || state == ServerState.Failed || state == ServerState.Crashed)
                {
                    _btnToggle.Text = "START SERVER";
                    _btnToggle.BackColor = Color.FromArgb(0, 120, 215);
                }

                _btnToggle.Enabled = (state != ServerState.Starting && state != ServerState.Stopping);
            }

            if (_trayIcon != null)
            {
                _trayIcon.Text = $"9Router Portable - {desc}";
            }

            AppendLog($"Server state: {desc}");
        }

        private void OnServerLogOutput(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnServerLogOutput(msg)));
                return;
            }
            AppendLog(msg);
        }

        private void AppendLog(string text)
        {
            if (_txtLog == null) return;
            if (_txtLog.Text.Length > 2000)
            {
                _txtLog.Text = _txtLog.Text.Substring(_txtLog.Text.Length - 1000);
            }
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to open browser: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}


