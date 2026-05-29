using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SonarEQChanger
{
    public partial class App : System.Windows.Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private TrayApplicationContext? _trayContext;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global crash logging
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => LogCrash("AppDomain", ev.ExceptionObject as Exception);
            this.DispatcherUnhandledException += (s, ev) => { LogCrash("Dispatcher", ev.Exception); ev.Handled = true; };

            try
            {
                // Prevent app from shutting down when MainWindow hides/closes
                this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Handle CLI arguments
                if (e.Args.Length > 0 && (e.Args[0] == "--discover" || e.Args[0] == "-d"))
                {
                    await DiscoverPresetsCliAsync();
                    this.Shutdown();
                    return;
                }

                // Start Tray and Watcher
                _trayContext = new TrayApplicationContext(this);

                // Enforce disabled audio devices at startup
                AudioDeviceEnforcer.EnforceDisabledDevices(_trayContext.Config.DisabledDevices);
            }
            catch (Exception ex)
            {
                LogCrash("OnStartup", ex);
                this.Shutdown();
            }
        }

        private void LogCrash(string source, Exception? ex)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            string message = $"[{DateTime.Now}] CRASH in {source}: {ex?.ToString() ?? "No exception details"}{Environment.NewLine}{Environment.NewLine}";
            try
            {
                File.AppendAllText(logPath, message);
            }
            catch { }
        }

        private async Task DiscoverPresetsCliAsync()
        {
            AttachConsole(-1);
            
            // Redirect standard output to console
            using var writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine(" SteelSeries Sonar EQ Preset Discovery");
            Console.WriteLine("========================================");
            
            try
            {
                var client = new SteelSeriesClient();
                Console.WriteLine("Finding SteelSeries GG API port...");
                string address = await client.GetSonarAddressAsync();
                Console.WriteLine($"Sonar Web Server found at: {address}");
                Console.WriteLine("Fetching presets list...");

                var list = await client.GetConfigsAsync();
                Console.WriteLine("\nAvailable Sonar Presets (Game Channel):");
                Console.WriteLine("----------------------------------------");
                
                var sb = new StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine(" SteelSeries Sonar EQ Preset Discovery");
                sb.AppendLine("========================================");
                sb.AppendLine($"Sonar Web Server: {address}");
                sb.AppendLine("----------------------------------------");

                foreach (var item in list)
                {
                    if (item.virtualAudioDevice == "game")
                    {
                        string line = $"Name: {item.name,-30} | UUID: {item.id}";
                        Console.WriteLine(line);
                        sb.AppendLine(line);
                    }
                }
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("You can copy these UUIDs into your config.json rules.");
                sb.AppendLine("----------------------------------------");
                
                string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets_list.txt");
                await File.WriteAllTextAsync(outputPath, sb.ToString());
                Console.WriteLine($"Presets list saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during discovery: {ex.Message}");
            }
            
            Console.WriteLine("========================================");
            Console.WriteLine();
        }
    }
}
