using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SonarEQChanger
{
    public class SonarWatcher
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private readonly SteelSeriesClient _client;
        private readonly int _intervalMs;
        private readonly string _defaultPresetId;
        private readonly Dictionary<string, string> _rules;
        private readonly Action<string, string, string> _onPresetChanged; // ruleName, presetId, actualPresetName
        private readonly Action<string> _onActiveWindowChanged;
        private readonly Action<string> _onLog;

        private System.Threading.Timer? _timer;
        private string? _currentActiveProcess;
        private string? _currentPresetId;
        private string? _activeRuleProcess; // Keeps track of the last process that triggered a rule
        private bool _isProcessing = false;

        public SonarWatcher(
            SteelSeriesClient client,
            int intervalMs,
            string defaultPresetId,
            Dictionary<string, string> rules,
            Action<string, string, string> onPresetChanged,
            Action<string> onActiveWindowChanged,
            Action<string> onLog)
        {
            _client = client;
            _intervalMs = intervalMs;
            _defaultPresetId = defaultPresetId;
            // Case-insensitive comparison for process names
            _rules = new Dictionary<string, string>(rules, StringComparer.OrdinalIgnoreCase);
            _onPresetChanged = onPresetChanged;
            _onActiveWindowChanged = onActiveWindowChanged;
            _onLog = onLog;
        }

        public void Start()
        {
            _timer = new System.Threading.Timer(async _ => await TickAsync(), null, 0, _intervalMs);
            _onLog("Watcher started.");
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _onLog("Watcher stopped.");
        }

        private async Task TickAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    _isProcessing = false;
                    return;
                }

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0)
                {
                    _isProcessing = false;
                    return;
                }

                string processName = "";
                string exeName = "";

                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    processName = proc.ProcessName; // e.g. "cs2"
                    exeName = processName + ".exe"; // e.g. "cs2.exe"

                    // Try to get actual module name, ignore if access denied
                    try
                    {
                        string? mainModule = proc.MainModule?.ModuleName;
                        if (!string.IsNullOrEmpty(mainModule))
                        {
                            exeName = mainModule;
                        }
                    }
                    catch
                    {
                        // Fallback to processName + ".exe"
                    }
                }
                catch
                {
                    // If process exited or we can't open it, skip
                    _isProcessing = false;
                    return;
                }

                // If process is same, do nothing
                if (exeName.Equals(_currentActiveProcess, StringComparison.OrdinalIgnoreCase))
                {
                    _isProcessing = false;
                    return;
                }

                _currentActiveProcess = exeName;
                _onLog($"Active window changed to: {exeName}");
                _onActiveWindowChanged?.Invoke(exeName);

                string targetPresetId = _defaultPresetId;
                string targetRuleName = "Desktop";
                bool foundRuleForForeground = false;

                // 1. Check if FOREGROUND window matches a rule
                foreach (var rule in _rules)
                {
                    if (exeName.Equals(rule.Key, StringComparison.OrdinalIgnoreCase) || 
                        processName.Equals(rule.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPresetId = rule.Value;
                        targetRuleName = rule.Key;
                        _activeRuleProcess = rule.Key; // Remember the game causing the rule
                        foundRuleForForeground = true;
                        break;
                    }
                }

                // 2. If foreground doesn't match, check if our previous active rule process is STILL running
                if (!foundRuleForForeground && !string.IsNullOrEmpty(_activeRuleProcess))
                {
                    bool isStillRunning = false;
                    try
                    {
                        // Check if a process with that name or exe is running
                        string checkName = _activeRuleProcess.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                            ? _activeRuleProcess.Substring(0, _activeRuleProcess.Length - 4) 
                            : _activeRuleProcess;
                            
                        var procs = Process.GetProcessesByName(checkName);
                        if (procs.Length > 0)
                        {
                            isStillRunning = true;
                        }
                    }
                    catch { }

                    if (isStillRunning)
                    {
                        // Keep the rule active
                        targetPresetId = _rules[_activeRuleProcess];
                        targetRuleName = _activeRuleProcess;
                    }
                    else
                    {
                        // It closed. Revert to default.
                        _activeRuleProcess = null;
                        targetPresetId = _defaultPresetId;
                        targetRuleName = "Desktop";
                    }
                }

                // Check if we need to switch preset
                if (_currentPresetId != targetPresetId)
                {
                    _onLog($"Switching preset to rule: {targetRuleName} (ID: {targetPresetId})");
                    bool success = await _client.SetPresetAsync(targetPresetId);
                    if (success)
                    {
                        _currentPresetId = targetPresetId;
                        
                        // We need to fetch the actual preset name to pass it back to the UI
                        string actualPresetName = "Unknown EQ";
                        try
                        {
                            var configs = await _client.GetConfigsAsync();
                            var found = configs.Find(c => c.id == targetPresetId);
                            if (found != null) actualPresetName = found.name;
                        }
                        catch { }

                        _onPresetChanged(targetRuleName, targetPresetId, actualPresetName);
                    }
                    else
                    {
                        _onLog($"Failed to switch preset to: {targetRuleName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _onLog($"Error in watcher tick: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}
