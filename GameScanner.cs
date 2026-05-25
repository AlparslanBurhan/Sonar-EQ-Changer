using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace SonarEQChanger
{
    public class DiscoveredGame
    {
        public string ExeName { get; set; } = "";
        public string GameName { get; set; } = "";
        public string Source { get; set; } = "";  // "Steam", "Epic", "GOG", "EA", "Xbox"
    }

    public static class GameScanner
    {
        public static List<DiscoveredGame> ScanAllGames()
        {
            var results = new Dictionary<string, DiscoveredGame>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in ScanSteam())       AddIfNew(results, g);
            foreach (var g in ScanEpicGames())   AddIfNew(results, g);
            foreach (var g in ScanGOG())         AddIfNew(results, g);
            foreach (var g in ScanEAApp())       AddIfNew(results, g);
            foreach (var g in ScanUbisoft())     AddIfNew(results, g);
            foreach (var g in ScanXboxApp())     AddIfNew(results, g);
            foreach (var g in ScanRiotGames())   AddIfNew(results, g);

            return results.Values
                          .OrderBy(g => g.GameName)
                          .ToList();
        }

        private static void AddIfNew(Dictionary<string, DiscoveredGame> dict, DiscoveredGame g)
        {
            if (!string.IsNullOrEmpty(g.ExeName) && !dict.ContainsKey(g.ExeName))
                dict[g.ExeName] = g;
        }

        // ─── Steam ────────────────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanSteam()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                // Find Steam installation
                string? steamPath = null;
                using var steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam") ??
                                     Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                if (steamKey != null)
                    steamPath = steamKey.GetValue("SteamPath") as string;

                if (string.IsNullOrEmpty(steamPath)) return results;

                // Read libraryfolders.vdf to get all library paths
                var libFolders = new List<string> { Path.Combine(steamPath, "steamapps") };
                string vdfPath = Path.Combine(steamPath, "config", "libraryfolders.vdf");
                if (!File.Exists(vdfPath))
                    vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

                if (File.Exists(vdfPath))
                {
                    string content = File.ReadAllText(vdfPath);
                    // Parse VDF for "path" values
                    foreach (var line in content.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = trimmed.Split('"');
                            if (parts.Length >= 4)
                            {
                                string p = parts[3].Replace("\\\\", "\\");
                                string steamapps = Path.Combine(p, "steamapps");
                                if (Directory.Exists(steamapps) && !libFolders.Contains(steamapps, StringComparer.OrdinalIgnoreCase))
                                    libFolders.Add(steamapps);
                            }
                        }
                    }
                }

                // Scan each library for appmanifest_*.acf files
                foreach (string lib in libFolders)
                {
                    if (!Directory.Exists(lib)) continue;
                    foreach (string acf in Directory.GetFiles(lib, "appmanifest_*.acf"))
                    {
                        try
                        {
                            string acfContent = File.ReadAllText(acf);
                            string gameName = ExtractVdfValue(acfContent, "name");
                            string installDir = ExtractVdfValue(acfContent, "installdir");
                            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(installDir)) continue;

                            string gameFolder = Path.Combine(lib, "common", installDir);
                            if (!Directory.Exists(gameFolder)) continue;

                            // Find main executable: pick the largest .exe at root level
                            string? mainExe = FindMainExe(gameFolder, gameName);
                            if (mainExe != null)
                            {
                                results.Add(new DiscoveredGame
                                {
                                    ExeName = mainExe,
                                    GameName = gameName,
                                    Source = "Steam"
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return results;
        }

        // ─── Epic Games ───────────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanEpicGames()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                string manifestsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");

                if (!Directory.Exists(manifestsPath)) return results;

                foreach (string itemFile in Directory.GetFiles(manifestsPath, "*.item"))
                {
                    try
                    {
                        string json = File.ReadAllText(itemFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "";
                        string launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() ?? "" : "";
                        string installLoc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";

                        // Skip launchers
                        if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(launchExe)) continue;
                        if (displayName.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) continue;
                        if (launchExe.Contains("Launcher", StringComparison.OrdinalIgnoreCase)) continue;

                        string exeName = Path.GetFileName(launchExe);
                        if (string.IsNullOrEmpty(exeName)) continue;

                        results.Add(new DiscoveredGame
                        {
                            ExeName = exeName,
                            GameName = displayName,
                            Source = "Epic"
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        // ─── GOG ──────────────────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanGOG()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                using var gogKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games") ??
                                   Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");
                if (gogKey == null) return results;

                foreach (string subKeyName in gogKey.GetSubKeyNames())
                {
                    try
                    {
                        using var gameKey = gogKey.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        string gameName = gameKey.GetValue("gameName") as string ?? gameKey.GetValue("GAMENAME") as string ?? "";
                        string exePath = gameKey.GetValue("exe") as string ?? gameKey.GetValue("EXE") as string ?? "";
                        string gamePath = gameKey.GetValue("path") as string ?? gameKey.GetValue("PATH") as string ?? "";

                        if (string.IsNullOrEmpty(gameName)) continue;

                        string? exeName = null;
                        if (!string.IsNullOrEmpty(exePath))
                            exeName = Path.GetFileName(exePath);
                        else if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
                            exeName = FindMainExe(gamePath, gameName);

                        if (string.IsNullOrEmpty(exeName)) continue;

                        // Filter out launchers/installers
                        if (IsSystemExe(exeName)) continue;

                        results.Add(new DiscoveredGame
                        {
                            ExeName = exeName,
                            GameName = gameName,
                            Source = "GOG"
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        // ─── EA App / Origin ──────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanEAApp()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                // EA stores game manifests in ProgramData
                string eaManifests = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EA", "EADesktop", "Manifests");

                string[] roots = { eaManifests,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "LocalContent") };

                foreach (string manifestDir in roots)
                {
                    if (!Directory.Exists(manifestDir)) continue;
                    foreach (string mf in Directory.GetFiles(manifestDir, "*.mfst", SearchOption.AllDirectories))
                    {
                        try
                        {
                            string content = File.ReadAllText(mf);
                            // MFST files use URL-encoded key=value pairs
                            // Look for &dipinstallpath= and &id=
                            string? installPath = ExtractQueryParam(content, "dipinstallpath");
                            string? gameId = ExtractQueryParam(content, "id");

                            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath)) continue;

                            string? exeName = FindMainExe(installPath, gameId ?? "");
                            if (string.IsNullOrEmpty(exeName)) continue;

                            results.Add(new DiscoveredGame
                            {
                                ExeName = exeName,
                                GameName = gameId ?? exeName,
                                Source = "EA"
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return results;
        }

        // ─── Ubisoft Connect ──────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanUbisoft()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                using var ubisoftKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs") ??
                                       Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
                if (ubisoftKey == null) return results;

                foreach (string subKeyName in ubisoftKey.GetSubKeyNames())
                {
                    try
                    {
                        using var gameKey = ubisoftKey.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        string? installDir = gameKey.GetValue("InstallDir") as string;
                        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) continue;

                        string? exeName = FindMainExe(installDir, "");
                        if (string.IsNullOrEmpty(exeName)) continue;

                        results.Add(new DiscoveredGame
                        {
                            ExeName = exeName,
                            GameName = exeName.Replace(".exe", ""),
                            Source = "Ubisoft"
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        // ─── Xbox / Microsoft Store ───────────────────────────────────────────
        private static List<DiscoveredGame> ScanXboxApp()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                // Xbox/GamePass games are typically in WindowsApps - we need to check registry
                using var xboxKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\GamingServices\PackageRepository\Root");
                if (xboxKey == null) return results;

                foreach (string subKeyName in xboxKey.GetSubKeyNames())
                {
                    try
                    {
                        using var gameKey = xboxKey.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        string? path = gameKey.GetValue("Root") as string;
                        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;

                        string? exeName = FindMainExe(path, "");
                        if (string.IsNullOrEmpty(exeName)) continue;
                        if (IsSystemExe(exeName)) continue;

                        results.Add(new DiscoveredGame
                        {
                            ExeName = exeName,
                            GameName = exeName.Replace(".exe", ""),
                            Source = "Xbox"
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        // ─── Riot Games ───────────────────────────────────────────────────────
        private static List<DiscoveredGame> ScanRiotGames()
        {
            var results = new List<DiscoveredGame>();
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string riotMetadata = Path.Combine(programData, "Riot Games", "Metadata");
                if (Directory.Exists(riotMetadata))
                {
                    foreach (var dir in Directory.GetDirectories(riotMetadata))
                    {
                        string gameId = Path.GetFileName(dir);
                        string yamlPath = Path.Combine(dir, $"{gameId}.product_settings.yaml");
                        if (File.Exists(yamlPath))
                        {
                            string content = File.ReadAllText(yamlPath);
                            string installPath = "";
                            foreach (var line in content.Split('\n'))
                            {
                                if (line.Contains("product_install_full_path:"))
                                {
                                    installPath = line.Split(new[] { "product_install_full_path:" }, StringSplitOptions.None)[1].Trim().Trim('"', '\'');
                                    break;
                                }
                            }
                            if (Directory.Exists(installPath))
                            {
                                string? exeName = FindMainExe(installPath, "");
                                if (!string.IsNullOrEmpty(exeName))
                                {
                                    results.Add(new DiscoveredGame
                                    {
                                        ExeName = exeName,
                                        GameName = gameId.Replace(".live", "").Replace("_", " "),
                                        Source = "Riot Games"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return results;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Find the main game executable in a folder. Prefers the largest .exe at root level,
        /// filtering out common non-game executables.
        /// </summary>
        private static string? FindMainExe(string folder, string gameName)
        {
            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 4
                };
                
                var exes = Directory.GetFiles(folder, "*.exe", options)
                    .Where(f => !IsSystemExe(Path.GetFileName(f)))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .ToList();

                // If game name provided, try to find an exe that matches the name
                if (!string.IsNullOrEmpty(gameName))
                {
                    string cleanName = gameName.ToLower()
                        .Replace(" ", "").Replace(":", "").Replace("-", "").Replace("'", "");

                    var nameMatch = exes.FirstOrDefault(e =>
                    {
                        string en = Path.GetFileNameWithoutExtension(e).ToLower()
                            .Replace(" ", "").Replace("_", "").Replace("-", "");
                        return en.Contains(cleanName) || cleanName.Contains(en);
                    });

                    if (nameMatch != null) return Path.GetFileName(nameMatch);
                }

                return exes.Any() ? Path.GetFileName(exes[0]) : null;
            }
            catch { return null; }
        }

        private static bool IsSystemExe(string exeName)
        {
            string lower = exeName.ToLower();

            // Partial-match blacklist — any exe whose name contains these words is not a game
            string[] partials = {
                "unins", "setup", "install", "update", "updater", "patcher", "patch",
                "launcher", "crash", "report", "helper", "support", "tool", "tools",
                "vc_redist", "dxsetup", "oalinst", "dotnet", "redistrib", "vcredist",
                "physx", "easyanticheat", "battleye", "beclient", "beclauncher",
                "steam", "galaxyclient", "gog", "origin", "eadesktop", "ubisoft",
                "ubilauncher", "epicgames", "service", "daemon", "tray", "agent",
                "cef", "qt5", "qt6", "directx", "d3d", "openal", "xna", "msvc",
                "msvcp", "msvcr", "python", "node", "java", "jre", "jdk", "ruby",
                "register", "config", "configurator", "benchmark", "diag", "diagnos",
                "repair", "remove", "uninstall", "cleanup", "cleaner", "anticheat",
                "eac", "be_", "inject", "overlay", "hook", "mod", "editor",
                "sdk", "devkit", "compile", "build", "test", "debug", "server",
                "dedicated", "mapeditor", "worldeditor", "leveleditor",
                "nvidia", "amd", "intel", "geforce", "radeon",
                "microsoft", "windows", "system32", "syswow",
                "chrome", "firefox", "edge", "opera", "brave", "browser",
                "discord", "slack", "teams", "zoom", "skype", "telegram",
                "spotify", "vlc", "media", "player", "photo", "image",
                "office", "word", "excel", "outlook", "onenote", "powerpoint",
                "adobe", "acrobat", "photoshop", "illustrator",
                "antivirus", "malware", "defender", "firewall",
                "7z", "winrar", "zip", "archive",
                "notepad", "calc", "paint", "mspaint"
            };

            return partials.Any(p => lower.Contains(p));
        }

        private static string ExtractVdfValue(string content, string key)
        {
            string search = $"\"{key}\"";
            int idx = content.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int valStart = content.IndexOf('"', idx + search.Length);
            if (valStart < 0) return "";
            int valEnd = content.IndexOf('"', valStart + 1);
            if (valEnd < 0) return "";
            return content.Substring(valStart + 1, valEnd - valStart - 1);
        }

        private static string? ExtractQueryParam(string content, string param)
        {
            string search = param + "=";
            int idx = content.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = content.IndexOf('&', start);
            string val = end < 0 ? content.Substring(start) : content.Substring(start, end - start);
            return Uri.UnescapeDataString(val.Trim());
        }
    }
}
