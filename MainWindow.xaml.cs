using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
// Resolve WinForms / WPF ambiguities
using MessageBox   = System.Windows.MessageBox;
using Color        = System.Windows.Media.Color;
using Brush        = System.Windows.Media.Brush;
using Brushes      = System.Windows.Media.Brushes;
using FontFamily   = System.Windows.Media.FontFamily;
using Rectangle    = System.Windows.Shapes.Rectangle;
using Cursors      = System.Windows.Input.Cursors;
using Orientation  = System.Windows.Controls.Orientation;
using Button       = System.Windows.Controls.Button;
using ComboBox     = System.Windows.Controls.ComboBox;

namespace SonarEQChanger
{
    public partial class MainWindow : Window
    {
        // ── State ──────────────────────────────────────────────────────
        private readonly SteelSeriesClient _client;
        private readonly TrayApplicationContext _context;
        private List<SonarConfig> _availablePresets = new();

        // ── Init ───────────────────────────────────────────────────────
        public MainWindow(TrayApplicationContext context, SteelSeriesClient client)
        {
            InitializeComponent();
            _context = context;
            _client = client;

            LoadInitialConfig();
            _ = LoadSonarPresetsAsync();
        }

        // ══════════════════════════════════════════════════════════════
        //  Window chrome
        // ══════════════════════════════════════════════════════════════
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Hide();

        private void BtnHide_Click(object sender, RoutedEventArgs e)
            => Hide();

        // ══════════════════════════════════════════════════════════════
        //  Sidebar Navigation
        // ══════════════════════════════════════════════════════════════
        private void BtnMenuToggle_Click(object sender, RoutedEventArgs e)
        {
            // Future: toggle expanded/collapsed sidebar
        }

        private void BtnNavHome_Click(object sender, RoutedEventArgs e)
            => ShowPage("profiles");

        private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
            => ShowPage("settings");

        private void BtnNavAbout_Click(object sender, RoutedEventArgs e)
            => ShowPage("about");

        private void ShowPage(string page)
        {
            pageProfiles.Visibility  = page == "profiles"  ? Visibility.Visible : Visibility.Collapsed;
            pageSettings.Visibility  = page == "settings"  ? Visibility.Visible : Visibility.Collapsed;
            pageAbout.Visibility     = page == "about"     ? Visibility.Visible : Visibility.Collapsed;

            rectHomeActive.Visibility     = page == "profiles"  ? Visibility.Visible : Visibility.Collapsed;
            rectSettingsActive.Visibility = page == "settings"  ? Visibility.Visible : Visibility.Collapsed;

            btnNavHome.Foreground     = page == "profiles"
                ? (Brush)FindResource("TxtAccent")
                : (Brush)FindResource("TxtSecond");
            btnNavSettings.Foreground = page == "settings"
                ? (Brush)FindResource("TxtAccent")
                : (Brush)FindResource("TxtSecond");
        }

        // ══════════════════════════════════════════════════════════════
        //  Data Loading
        // ══════════════════════════════════════════════════════════════
        private void LoadInitialConfig()
        {
            try
            {
                txtInterval.Text   = _context.Config.CheckIntervalMilliseconds.ToString();
                chkStartup.IsChecked = _context.Config.StartWithWindows;

                foreach (ComboBoxItem item in cbLanguage.Items)
                {
                    if (item.Tag?.ToString() == _context.Config.Language)
                    { cbLanguage.SelectedItem = item; break; }
                }

                ApplyLanguage();
                RenderRulesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Config load error: {ex.Message}");
            }
        }

        private async Task LoadSonarPresetsAsync()
        {
            txtConnectionStatus.Text = Loc.Get("Connecting");
            ledStatus.Fill = new SolidColorBrush(Color.FromRgb(120, 120, 160));

            try
            {
                string address = await _client.GetSonarAddressAsync();
                txtConnectionStatus.Text = $"● {address.Replace("http://", "")}";
                ledStatus.Fill = new SolidColorBrush(Color.FromRgb(78, 201, 126));

                _availablePresets = await _client.GetConfigsAsync();

                cbDefaultPreset.Items.Clear();

                foreach (var preset in _availablePresets)
                {
                    if (preset.virtualAudioDevice == "game")
                    {
                        cbDefaultPreset.Items.Add(new PresetComboBoxItem { Text = preset.name, Value = preset.id });
                    }
                }

                if (!string.IsNullOrEmpty(_context.Config.DefaultPresetId))
                    SelectComboBoxByValue(cbDefaultPreset, _context.Config.DefaultPresetId);

                RenderRulesList();
            }
            catch (Exception ex)
            {
                txtConnectionStatus.Text = Loc.Get("ConnFailed");
                ledStatus.Fill = new SolidColorBrush(Color.FromRgb(224, 85, 85));
                txtActivePreset.Text = ex.Message;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Rules List Rendering (Inline Design)
        // ══════════════════════════════════════════════════════════════
        private void RenderRulesList()
        {
            spRules.Children.Clear();

            foreach (var rule in _context.Config.Rules)
            {
                spRules.Children.Add(BuildRuleItem(rule.Key, rule.Value));
            }

            // Empty row for adding a new rule
            spRules.Children.Add(BuildAddRow());
        }

        private List<string> GetKnownExeNames()
        {
            var list = new List<string>();
            foreach (var exe in _context.Config.DiscoveredGames)
            {
                if (_context.Config.DiscoveredGameNames.TryGetValue(exe, out string name) && !string.IsNullOrEmpty(name))
                    list.Add($"{name} ({exe})");
                else
                    list.Add(exe);
            }
            return list.OrderBy(x => x).ToList();
        }

        private UIElement BuildRuleItem(string originalKey, string currentPresetId)
        {
            var rowBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("Border"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 12, 0, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Exe
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });                   // Arrow
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Preset
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // Delete

            // Exe ComboBox
            string displayKey = originalKey;
            if (_context.Config.DiscoveredGameNames.TryGetValue(originalKey, out string gameName) && !string.IsNullOrEmpty(gameName))
                displayKey = $"{gameName} ({originalKey})";

            var cbExe = new ComboBox { IsEditable = false };
            var knownExes = GetKnownExeNames();
            if (!knownExes.Contains(displayKey))
                knownExes.Insert(0, displayKey);

            foreach (var exe in knownExes) cbExe.Items.Add(exe);
            cbExe.SelectedItem = displayKey;
            
            Grid.SetColumn(cbExe, 0);
            grid.Children.Add(cbExe);

            // Arrow
            var arrow = new TextBlock
            {
                Text = "→",
                Foreground = (Brush)FindResource("TxtSecond"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);

            // Preset ComboBox
            var cbPreset = new ComboBox { DisplayMemberPath = "Text", SelectedValuePath = "Value" };
            foreach (var p in _availablePresets.Where(x => x.virtualAudioDevice == "game"))
            {
                cbPreset.Items.Add(new PresetComboBoxItem { Text = p.name, Value = p.id });
            }
            if (!string.IsNullOrEmpty(currentPresetId))
                SelectComboBoxByValue(cbPreset, currentPresetId);
            Grid.SetColumn(cbPreset, 2);
            grid.Children.Add(cbPreset);

            // Delete Button
            var btnDel = new Button
            {
                Content = "🗑",
                Style = (Style)FindResource("DangerButton"),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnDel.Click += (s, e) =>
            {
                _context.Config.Rules.Remove(originalKey);
                _context.SaveConfig();
                _context.ReloadConfig();
                RenderRulesList();
            };
            Grid.SetColumn(btnDel, 3);
            grid.Children.Add(btnDel);

            // Save logic
            Action saveRule = () =>
            {
                string newKey = cbExe.Text.Trim();
                if (newKey.EndsWith(")") && newKey.Contains("("))
                {
                    int start = newKey.LastIndexOf('(');
                    newKey = newKey.Substring(start + 1, newKey.Length - start - 2).Trim();
                }

                var selectedPreset = cbPreset.SelectedItem as PresetComboBoxItem;

                if (string.IsNullOrEmpty(newKey) || selectedPreset == null) return;

                if (newKey != originalKey)
                {
                    _context.Config.Rules.Remove(originalKey);
                    if (!_context.Config.DiscoveredGames.Contains(newKey, StringComparer.OrdinalIgnoreCase))
                        _context.Config.DiscoveredGames.Add(newKey);
                }

                _context.Config.Rules[newKey] = selectedPreset.Value;
                _context.SaveConfig();
                _context.ReloadConfig();
                
                // If key changed, we might need to re-render to update originalKey closures,
                // but let's just re-render to be safe.
                if (newKey != originalKey)
                {
                    RenderRulesList();
                }
            };

            cbExe.LostFocus += (s, e) => saveRule();
            cbPreset.SelectionChanged += (s, e) => saveRule();

            rowBorder.Child = grid;
            return rowBorder;
        }

        private UIElement BuildAddRow()
        {
            var rowBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("Border"),
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(0, 12, 0, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cbExe = new ComboBox { IsEditable = false };
            foreach (var exe in GetKnownExeNames()) cbExe.Items.Add(exe);
            Grid.SetColumn(cbExe, 0);
            grid.Children.Add(cbExe);

            var arrow = new TextBlock
            {
                Text = "→",
                Foreground = (Brush)FindResource("TxtSecond"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);

            var cbPreset = new ComboBox { DisplayMemberPath = "Text", SelectedValuePath = "Value" };
            foreach (var p in _availablePresets.Where(x => x.virtualAudioDevice == "game"))
            {
                cbPreset.Items.Add(new PresetComboBoxItem { Text = p.name, Value = p.id });
            }
            Grid.SetColumn(cbPreset, 2);
            grid.Children.Add(cbPreset);

            var btnAdd = new Button
            {
                Content = "+ Ekle",
                Style = (Style)FindResource("AccentButton"),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            Action addRule = () =>
            {
                string newKey = cbExe.Text.Trim();
                if (newKey.EndsWith(")") && newKey.Contains("("))
                {
                    int start = newKey.LastIndexOf('(');
                    newKey = newKey.Substring(start + 1, newKey.Length - start - 2).Trim();
                }

                var selectedPreset = cbPreset.SelectedItem as PresetComboBoxItem;

                if (string.IsNullOrEmpty(newKey) || selectedPreset == null) return;

                _context.Config.Rules[newKey] = selectedPreset.Value;
                if (!_context.Config.DiscoveredGames.Contains(newKey, StringComparer.OrdinalIgnoreCase))
                    _context.Config.DiscoveredGames.Add(newKey);

                _context.SaveConfig();
                _context.ReloadConfig();
                RenderRulesList(); // Re-render everything to convert this row to a normal rule
            };

            btnAdd.Click += (s, e) => addRule();
            Grid.SetColumn(btnAdd, 3);
            grid.Children.Add(btnAdd);

            rowBorder.Child = grid;
            return rowBorder;
        }

        // ══════════════════════════════════════════════════════════════
        //  UI Event Handlers – Profiles Page
        // ══════════════════════════════════════════════════════════════

        private void CbDefaultPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDefaultPreset.SelectedItem is PresetComboBoxItem item)
            {
                _context.Config.DefaultPresetId = item.Value;
                _context.SaveConfig();
                _context.ReloadConfig();
            }
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            string? key = (sender as Button)?.Tag as string;
            if (string.IsNullOrEmpty(key)) return;

            _context.Config.Rules.Remove(key);
            _context.SaveConfig();
            _context.ReloadConfig();
            RenderRulesList();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _client.ResetAddress();
            await LoadSonarPresetsAsync();
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            lblScanBtn.Text = "Scanning...";
            btnScan.IsEnabled = false;

            Task.Run(() =>
            {
                var games = GameScanner.ScanAllGames();
                Dispatcher.Invoke(() =>
                {
                    int added = 0;
                    foreach (var g in games)
                    {
                        if (!_context.Config.DiscoveredGames.Contains(g.ExeName, StringComparer.OrdinalIgnoreCase))
                        {
                            _context.Config.DiscoveredGames.Add(g.ExeName);
                            added++;
                        }
                        if (!string.IsNullOrEmpty(g.GameName))
                        {
                            _context.Config.DiscoveredGameNames[g.ExeName] = g.GameName;
                        }
                    }
                    _context.SaveConfig();
                    RenderRulesList();
                    lblScanBtn.Text = Loc.Get("BtnScan");
                    btnScan.IsEnabled = true;

                    if (added > 0)
                    {
                        txtActivePreset.Text = Loc.Get("ScanDone").Replace("{0}", added.ToString());
                        ledActive.Fill = new SolidColorBrush(Color.FromRgb(78, 201, 126));
                    }
                    else
                    {
                        txtActivePreset.Text = Loc.Get("ScanNone");
                        ledActive.Fill = new SolidColorBrush(Color.FromRgb(120, 120, 160));
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  Events – Settings Page
        // ══════════════════════════════════════════════════════════════
        private void AutoSaveSettings()
        {
            if (!IsLoaded || _context == null) return;

            if (int.TryParse(txtInterval.Text, out int interval))
                _context.Config.CheckIntervalMilliseconds = interval;

            _context.Config.StartWithWindows = chkStartup.IsChecked == true;

            if (cbLanguage.SelectedItem is ComboBoxItem langItem)
                _context.Config.Language = langItem.Tag?.ToString() ?? "TR";

            _context.SaveConfig();
            _context.ReloadConfig();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e) 
        {
            AutoSaveSettings();
        }

        private void TxtInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            AutoSaveSettings();
        }

        private void CbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLanguage.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string lang = item.Tag.ToString() ?? "TR";
                Loc.CurrentLang = lang;
                _context.Config.Language = lang;
                ApplyLanguage();
                AutoSaveSettings();
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtInterval.Text, out int interval))
                _context.Config.CheckIntervalMilliseconds = interval;

            _context.Config.StartWithWindows = chkStartup.IsChecked == true;

            if (cbLanguage.SelectedItem is ComboBoxItem langItem)
                _context.Config.Language = langItem.Tag?.ToString() ?? "TR";

            _context.SaveConfig();
            _context.ReloadConfig();

            // Brief feedback on button
            lblSaveBtn.Text = "✓ Saved";
            Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => lblSaveBtn.Text = Loc.Get("BtnSave")));
        }

        private void BtnClearGames_Click(object sender, RoutedEventArgs e)
        {
            _context.Config.DiscoveredGames.Clear();
            _context.Config.DiscoveredGameNames.Clear();
            _context.SaveConfig();
            RenderRulesList(); // refresh comboboxes
            MessageBox.Show(Loc.Get("ClearedSuccess"), Loc.Get("ClearedSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files|*.exe",
                Title = Loc.Get("ManualAdd") ?? "Select EXE File"
            };

            if (dialog.ShowDialog() == true)
            {
                txtManualExe.Text = System.IO.Path.GetFileName(dialog.FileName);
                if (string.IsNullOrEmpty(txtManualName.Text))
                {
                    txtManualName.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        private void BtnAddManual_Click(object sender, RoutedEventArgs e)
        {
            string exe = txtManualExe.Text.Trim();
            string name = txtManualName.Text.Trim();

            if (string.IsNullOrEmpty(exe)) return;

            if (!exe.ToLower().EndsWith(".exe"))
                exe += ".exe";

            if (!_context.Config.DiscoveredGames.Contains(exe, StringComparer.OrdinalIgnoreCase))
                _context.Config.DiscoveredGames.Add(exe);

            if (!string.IsNullOrEmpty(name))
                _context.Config.DiscoveredGameNames[exe] = name;
            else if (!_context.Config.DiscoveredGameNames.ContainsKey(exe))
                _context.Config.DiscoveredGameNames[exe] = "";

            _context.SaveConfig();
            RenderRulesList();
            
            txtManualExe.Text = "";
            txtManualName.Text = "";
            
            MessageBox.Show(Loc.Get("ManualSuccess"), Loc.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════
        //  Localization
        // ══════════════════════════════════════════════════════════════
        private void ApplyLanguage()
        {
            lblPageHeader.Text      = Loc.Get("RulesHeader");
            lblDefaultConfig.Text   = Loc.Get("DefaultEQ");
            lblPerApp.Text          = Loc.Get("PerAppConfig");
            lblSettingsHeader.Text  = Loc.Get("Settings");
            lblStartWithWin.Text    = Loc.Get("StartWithWin");
            lblLanguage.Text        = Loc.Get("Language");
            lblScanInterval.Text    = Loc.Get("ScanInterval");
            lblScanIntervalDesc.Text = Loc.Get("ScanIntervalDesc");
            lblSaveBtn.Text         = Loc.Get("BtnSave");
            lblScanBtn.Text         = Loc.Get("BtnScan");
            lblGeneralSection.Text  = Loc.Get("GeneralSection");
            lblStartWithWinDesc.Text = Loc.Get("StartWithWinDesc");
            lblClearScannedGames.Text = Loc.Get("ClearScannedGames");
            lblClearScannedGamesDesc.Text = Loc.Get("ClearScannedGamesDesc");
            lblBtnClear.Text        = Loc.Get("BtnClear");
            lblManualAdd.Text       = Loc.Get("ManualAdd");
            lblManualAddDesc.Text   = Loc.Get("ManualAddDesc");
            lblManualExe.Text       = Loc.Get("ManualExe");
            lblManualName.Text      = Loc.Get("ManualName");
            lblBtnAddManual.Text    = Loc.Get("BtnAddManual");
            RenderRulesList();
        }

        // ══════════════════════════════════════════════════════════════
        //  Thread-safe callbacks from SonarWatcher
        // ══════════════════════════════════════════════════════════════
        public void UpdateActiveWindow(string windowName)
        {
            // no-op: status displayed via UpdateActivePreset
        }

        public void UpdateActivePreset(string ruleName, string presetName)
        {
            if (!Dispatcher.CheckAccess())
            { Dispatcher.BeginInvoke(new Action<string, string>(UpdateActivePreset), ruleName, presetName); return; }

            bool isGame = ruleName != "Desktop";
            string label = isGame
                ? $"{ruleName}  →  {presetName}"
                : $"{Loc.Get("Desktop")} ({presetName})";

            txtActivePreset.Text       = label;
            txtActivePreset.Foreground = isGame
                ? new SolidColorBrush(Color.FromRgb(78, 201, 126))
                : new SolidColorBrush(Color.FromRgb(120, 120, 160));
            ledActive.Fill = isGame
                ? new SolidColorBrush(Color.FromRgb(78, 201, 126))
                : new SolidColorBrush(Color.FromRgb(120, 120, 160));
        }

        // ══════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════
        private string ResolvePresetName(string presetId)
        {
            var found = _availablePresets.Find(p => p.id == presetId);
            return found?.name ?? presetId;
        }

        private static void SelectComboBoxByValue(ComboBox cb, string value)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is PresetComboBoxItem item && item.Value == value)
                { cb.SelectedIndex = i; return; }
            }
        }
    }

    public class PresetComboBoxItem
    {
        public string Text  { get; set; } = "";
        public string Value { get; set; } = "";
        public override string ToString() => Text;
    }
}
