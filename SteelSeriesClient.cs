using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SonarEQChanger
{
    public class SteelSeriesClient
    {
        private readonly HttpClient _httpClient;
        private string? _sonarAddress;

        public SteelSeriesClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public string GetCorePropsPath()
        {
            string[] possiblePaths = new[]
            {
                @"C:\ProgramData\SteelSeries\GG\coreProps.json",
                @"C:\ProgramData\SteelSeries\SteelSeries Engine 3\coreProps.json"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("SteelSeries coreProps.json file not found in ProgramData paths.");
        }

        public void ResetAddress() { _sonarAddress = null; }

        public async Task<string> GetSonarAddressAsync()
        {
            if (!string.IsNullOrEmpty(_sonarAddress))
            {
                return _sonarAddress;
            }

            string corePropsPath = GetCorePropsPath();
            string jsonContent = await File.ReadAllTextAsync(corePropsPath);
            
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string ggAddress = "";
            if (root.TryGetProperty("ggEncryptedAddress", out var ggAddrProp))
            {
                ggAddress = ggAddrProp.GetString() ?? "";
            }
            else if (root.TryGetProperty("address", out var addrProp))
            {
                ggAddress = addrProp.GetString() ?? "";
            }

            if (string.IsNullOrEmpty(ggAddress))
            {
                throw new Exception("Could not find a valid API address in coreProps.json");
            }

            // The address is ggEncryptedAddress, so it uses HTTPS
            string subAppsUrl = $"https://{ggAddress}/subApps";
            
            HttpResponseMessage response = await _httpClient.GetAsync(subAppsUrl);
            response.EnsureSuccessStatusCode();
            
            string subAppsJson = await response.Content.ReadAsStringAsync();
            using var subAppsDoc = JsonDocument.Parse(subAppsJson);
            var subAppsRoot = subAppsDoc.RootElement;

            JsonElement sonarMetadata;
            if (subAppsRoot.TryGetProperty("subApps", out var subAppsElement) && 
                subAppsElement.TryGetProperty("sonar", out var sonarElement) &&
                sonarElement.TryGetProperty("metadata", out var metadataElement))
            {
                sonarMetadata = metadataElement;
            }
            else if (subAppsRoot.TryGetProperty("sonar", out var directSonarElement) &&
                     directSonarElement.TryGetProperty("metadata", out var directMetadataElement))
            {
                sonarMetadata = directMetadataElement;
            }
            else
            {
                throw new Exception("Could not find Sonar sub-app metadata in subApps response.");
            }

            if (sonarMetadata.TryGetProperty("webServerAddress", out var webServerAddressProp))
            {
                string webAddr = webServerAddressProp.GetString() ?? "";
                if (!webAddr.StartsWith("http://") && !webAddr.StartsWith("https://"))
                {
                    webAddr = $"http://{webAddr}"; // default to http for Sonar local web server
                }
                _sonarAddress = webAddr;
                return _sonarAddress;
            }

            throw new Exception("webServerAddress not found in Sonar metadata.");
        }

        public async Task<string> GetPresetsJsonAsync()
        {
            string baseAddress = await GetSonarAddressAsync();
            string url = $"{baseAddress}/configs";
            
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<bool> SetPresetAsync(string presetId)
        {
            string baseAddress = await GetSonarAddressAsync();
            string url = $"{baseAddress}/configs/{presetId}/select";

            var content = new StringContent("", Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _httpClient.PutAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // If there's a connection issue, clear cached address so we resolve it again next time
                _sonarAddress = null;
                throw;
            }
        }

        public async Task<System.Collections.Generic.List<SonarConfig>> GetConfigsAsync()
        {
            string json = await GetPresetsJsonAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var list = new System.Collections.Generic.List<SonarConfig>();
            
            JsonElement array = root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("presets", out var p)) array = p;
                else if (root.TryGetProperty("configs", out var c)) array = c;
            }

            if (array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    string id = "";
                    string name = "";
                    string device = "";
                    
                    if (item.TryGetProperty("id", out var idProp)) id = idProp.GetString() ?? "";
                    if (item.TryGetProperty("name", out var nameProp)) name = nameProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(id) && item.TryGetProperty("uuid", out var uuidProp)) id = uuidProp.GetString() ?? "";
                    if (item.TryGetProperty("virtualAudioDevice", out var devProp)) device = devProp.GetString() ?? "";

                    list.Add(new SonarConfig { id = id, name = name, virtualAudioDevice = device });
                }
            }
            return list;
        }
    }

    public class SonarConfig
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string virtualAudioDevice { get; set; } = "";
    }
}
