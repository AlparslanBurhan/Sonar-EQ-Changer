using System;
using System.Collections.Generic;

namespace SonarEQChanger
{
    public class AppConfig
    {
        public int CheckIntervalMilliseconds { get; set; } = 1000;
        public string DefaultPresetId { get; set; } = "";
        public Dictionary<string, string> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Language { get; set; } = "TR"; // "TR" or "EN"
        public bool StartWithWindows { get; set; } = false;
        public List<string> DiscoveredGames { get; set; } = new();
        public Dictionary<string, string> DiscoveredGameNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> DisabledDevices { get; set; } = new();
    }
}
