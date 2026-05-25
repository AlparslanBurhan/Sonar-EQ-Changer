using System.Collections.Generic;

namespace SonarEQChanger
{
    public static class Loc
    {
        public static string CurrentLang { get; set; } = "TR";

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            {
                "EN", new Dictionary<string, string>
                {
                    { "Title",          "Sonar Auto Switch" },
                    { "RulesHeader",    "Auto Switch Profiles" },
                    { "DefaultEQ",      "Default config:" },
                    { "PerAppConfig",   "Per app config:" },
                    { "ExeName",        "Game executable name:" },
                    { "ExeDesc",        "The name of the executable.\nYou can use the Task Manager to check." },
                    { "SonarConfig",    "Sonar gaming configuration:" },
                    { "SonarConfigDesc","The name of the Sonar Gaming Profile.\nYou can setup profiles in the GG app." },
                    { "Settings",       "Settings" },
                    { "GeneralSection", "GENERAL" },
                    { "BtnAddRule",     "Add game" },
                    { "BtnDeleteRule",  "Remove" },
                    { "BtnSave",        "Save" },
                    { "BtnScan",        "Scan Installed Games" },
                    { "StartWithWin",   "Start with Windows" },
                    { "StartWithWinDesc", "Automatically launches in tray on startup" },
                    { "Language",       "Language / Dil" },
                    { "ScanInterval",   "Scan interval (ms)" },
                    { "Connecting",     "Connecting..." },
                    { "ConnFailed",     "Connection Failed" },
                    { "Active",         "Active" },
                    { "Desktop",        "Desktop" },
                    { "ScanDone",       "{0} new games found" },
                    { "ScanNone",       "No new games found" },
                    { "WarningSelectProcess", "Please select or enter a process name." },
                    { "WarningSelectPreset",  "Please select a target EQ preset." },
                    { "SaveSuccess",    "Settings saved!" },
                    { "Info",           "Info" },
                    { "Warning",        "Warning" },
                    { "Error",          "Error" },
                    { "ClearScannedGames", "Clear Scanned Games" },
                    { "ClearScannedGamesDesc", "Clears all discovered game names in the background" },
                    { "ClearedSuccess", "The scanned games list has been successfully reset." },
                    { "ClearedSuccessTitle", "Cleared" },
                    { "ScanIntervalDesc", "How frequently to check the active window" },
                    { "BtnClear", "Reset" },
                    { "ManualAdd", "Add Game Manually" },
                    { "ManualAddDesc", "If not found in scan, type the EXE name manually" },
                    { "ManualExe", "EXE Name" },
                    { "ManualName", "Display Name (Optional)" },
                    { "BtnAddManual", "Add" },
                    { "ManualSuccess", "Game added!" }
                }
            },
            {
                "TR", new Dictionary<string, string>
                {
                    { "Title",          "Sonar Auto Switch" },
                    { "RulesHeader",    "Otomatik Geçiş Profilleri" },
                    { "DefaultEQ",      "Varsayılan profil:" },
                    { "PerAppConfig",   "Uygulamaya özel profil:" },
                    { "ExeName",        "Oyun exe adı:" },
                    { "ExeDesc",        "Çalıştırılabilir dosyanın adı.\nGörev Yöneticisi'nden kontrol edebilirsiniz." },
                    { "SonarConfig",    "Sonar oyun yapılandırması:" },
                    { "SonarConfigDesc","Sonar Oyun Profilinin adı.\nGG uygulamasında profil ayarlayabilirsiniz." },
                    { "Settings",       "Ayarlar" },
                    { "GeneralSection", "GENEL" },
                    { "BtnAddRule",     "Oyun ekle" },
                    { "BtnDeleteRule",  "Kaldır" },
                    { "BtnSave",        "Kaydet" },
                    { "BtnScan",        "Yüklü Oyunları Tara" },
                    { "StartWithWin",   "Windows ile Başlat" },
                    { "StartWithWinDesc", "Bilgisayar açılınca tray'de sessizce çalışır" },
                    { "Language",       "Dil / Language" },
                    { "ScanInterval",   "Tarama sıklığı (ms)" },
                    { "Connecting",     "Bağlanıyor..." },
                    { "ConnFailed",     "Bağlantı Başarısız" },
                    { "Active",         "Aktif" },
                    { "Desktop",        "Masaüstü" },
                    { "ScanDone",       "{0} yeni oyun bulundu" },
                    { "ScanNone",       "Yeni oyun bulunamadı" },
                    { "WarningSelectProcess", "Lütfen bir işlem adı seçin veya yazın." },
                    { "WarningSelectPreset",  "Lütfen bir hedef EQ profili seçin." },
                    { "SaveSuccess",    "Ayarlar kaydedildi!" },
                    { "Info",           "Bilgi" },
                    { "Warning",        "Uyarı" },
                    { "Error",          "Hata" },
                    { "ClearScannedGames", "Taranmış Oyunları Temizle" },
                    { "ClearScannedGamesDesc", "Arka planda keşfedilen tüm exe isimlerini sıfırlar" },
                    { "ClearedSuccess", "Taranmış oyun listesi başarıyla sıfırlandı." },
                    { "ClearedSuccessTitle", "Temizlendi" },
                    { "ScanIntervalDesc", "Aktif pencere ne sıklıkla kontrol edilsin" },
                    { "BtnClear", "Sıfırla" },
                    { "ManualAdd", "Manuel Oyun Ekle" },
                    { "ManualAddDesc", "Eğer taramada bulunamadıysa EXE adını elle yazın" },
                    { "ManualExe", "EXE Adı" },
                    { "ManualName", "Görünür Ad (İsteğe bağlı)" },
                    { "BtnAddManual", "Ekle" },
                    { "ManualSuccess", "Oyun eklendi!" }
                }
            }
        };

        public static string Get(string key)
        {
            if (Strings.TryGetValue(CurrentLang, out var langDict) &&
                langDict.TryGetValue(key, out var val))
                return val;

            if (Strings["EN"].TryGetValue(key, out var fallback))
                return fallback;

            return key;
        }
    }
}
