using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;

namespace SonarEQChanger
{
    public class TrayApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly SteelSeriesClient _client;
        private readonly Application _app;
        private SonarWatcher? _watcher;
        private MainWindow? _mainWindow;
        private string _configPath = "config.json";
        public AppConfig Config { get; private set; } = new();

        private string GetAppDataFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "SonarEQChanger");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public TrayApplicationContext(Application app)
        {
            _app = app;
            _client = new SteelSeriesClient();

            // 1. Create Tray Icon
            _trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/icon.ico")).Stream),
                Text = "Sonar EQ Changer",
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();

            // 2. Set Context Menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Settings", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add("Reload Config", null, (s, e) => LoadConfigAndStart());
            contextMenu.Items.Add("Discover Presets", null, async (s, e) => await DiscoverFromTray());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => Exit());
            _trayIcon.ContextMenuStrip = contextMenu;

            // 3. Load config and start watcher
            LoadConfigAndStart();
        }

        public void ReloadConfig()
        {
            LoadConfigAndStart();
        }

        private void ShowMainWindow()
        {
            // Run on WPF UI thread
            _app.Dispatcher.Invoke(() =>
            {
                if (_mainWindow == null || !_mainWindow.IsLoaded)
                {
                    _mainWindow = new MainWindow(this, _client);
                }

                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }
                _mainWindow.Activate();
                
                // Set window state back to normal if minimized
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }
            });
        }

        private void LoadConfigAndStart()
        {
            try
            {
                _watcher?.Stop();
                Log("Loading configuration...");

                if (!File.Exists(_configPath))
                {
                    _configPath = Path.Combine(GetAppDataFolder(), "config.json");
                    string oldConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    if (!File.Exists(_configPath) && File.Exists(oldConfigPath))
                    {
                        try
                        {
                            File.Copy(oldConfigPath, _configPath);
                            Log("Migrated old config.json to AppData folder.");
                        }
                        catch { }
                    }
                }

                if (!File.Exists(_configPath))
                {
                    // Write default config
                    Config = new AppConfig
                    {
                        DefaultPresetId = "CHANGE_ME_TO_DEFAULT_DESKTOP_PRESET_UUID",
                        Rules = { { "VALORANT-Win64-Shipping.exe", "CHANGE_ME" } }
                    };
                    SaveConfig();
                    Log($"Default config.json created at: {_configPath}");
                }
                else
                {
                    string configJson = File.ReadAllText(_configPath);
                    Config = JsonSerializer.Deserialize<AppConfig>(configJson) ?? new AppConfig();
                }

                // Apply Localization
                Loc.CurrentLang = Config.Language;

                // Apply Startup Registry
                SetStartup(Config.StartWithWindows);

                _watcher = new SonarWatcher(
                    _client,
                    Config.CheckIntervalMilliseconds,
                    Config.DefaultPresetId,
                    Config.Rules,
                    OnPresetChanged,
                    OnActiveWindowChanged,
                    Log
                );
                _watcher.Start();

                // 4. Enforce Disabled Audio Devices at startup
                AudioDeviceEnforcer.EnforceDisabledDevices(Config.DisabledDevices);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading config: {ex.Message}", "Sonar EQ Changer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                
                // Reapply settings that might have changed
                Loc.CurrentLang = Config.Language;
                SetStartup(Config.StartWithWindows);
            }
            catch (Exception ex)
            {
                Log($"Failed to save config: {ex.Message}");
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!;
                string appName = "SonarEQChanger";
                
                if (enable)
                {
                    string path = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(path))
                    {
                        key.SetValue(appName, $"\"{path}\"");
                    }
                }
                else
                {
                    if (key.GetValue(appName) != null)
                    {
                        key.DeleteValue(appName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"SetStartup error: {ex.Message}");
            }
        }

        private async Task DiscoverFromTray()
        {
            try
            {
                string address = await _client.GetSonarAddressAsync();
                var presets = await _client.GetConfigsAsync();
                
                var sb = new StringBuilder();
                sb.AppendLine("==================================================");
                sb.AppendLine(" SteelSeries Sonar EQ Preset Discovery Report");
                sb.AppendLine("==================================================");
                sb.AppendLine($"Report Date: {DateTime.Now}");
                sb.AppendLine($"Sonar Web Server: {address}");
                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine();

                foreach (var item in presets)
                {
                    if (item.virtualAudioDevice == "game")
                    {
                        sb.AppendLine($"Name: {item.name,-30} | UUID: {item.id}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine("Copy the UUID of the preset you want and paste it");
                sb.AppendLine("into config.json or configure it inside the GUI.");
                sb.AppendLine("==================================================");

                string outputPath = Path.Combine(GetAppDataFolder(), "presets_list.txt");
                await File.WriteAllTextAsync(outputPath, sb.ToString());

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{outputPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Discovery failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPresetChanged(string appName, string presetId, string presetName)
        {
            Log($"Active EQ Preset switched to match process: {appName} (Preset: {presetName})");
            
            // Only show notification if we are switching to a game (not Desktop)
            if (appName != "Desktop")
            {
                _trayIcon.ShowBalloonTip(2000, "Sonar EQ Changer", $"Preset switched: {appName} -> {presetName}", ToolTipIcon.Info);
            }
            
            // Dispatch to UI thread
            _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                _mainWindow?.UpdateActivePreset(appName, presetName);
            }));
        }

        private void OnActiveWindowChanged(string windowName)
        {
            // Dispatch to UI thread
            _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                _mainWindow?.UpdateActiveWindow(windowName);
            }));
        }

        private void Log(string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Debug.WriteLine(logLine);
            
            try
            {
                string logPath = Path.Combine(GetAppDataFolder(), "service.log");
                File.AppendAllText(logPath, logLine + Environment.NewLine);
            }
            catch { }
        }

        private void Exit()
        {
            _watcher?.Stop();
            _app.Dispatcher.Invoke(() =>
            {
                _mainWindow?.Close();
            });
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _app.Shutdown();
        }
    }
}
