using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SonarEQChanger
{
    public class AudioDeviceItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsPlayback { get; set; }
        public bool IsDisabled { get; set; }
    }

    internal static class AudioDeviceEnforcer
    {
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClient { }

        // Windows 7/8/early 10
        [ComImport, Guid("f8679f50-850a-41ce-ab6c-57360e68eb8a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat(string pszDeviceName, out IntPtr ppFormat);
            [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, out IntPtr ppFormat);
            [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
            [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
            [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, out IntPtr pmftDefaultPeriod, out IntPtr pmftMinimumPeriod);
            [PreserveSig] int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
            [PreserveSig] int GetShareMode(string pszDeviceName, out IntPtr pMode);
            [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
            [PreserveSig] int GetPropertyValue(string pszDeviceName, int bFxStore, IntPtr key, out IntPtr pv);
            [PreserveSig] int SetPropertyValue(string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
            [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, int role);
            [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
        }

        // Windows 11 / Modern Windows 10
        [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfigAlt
        {
            [PreserveSig] int GetMixFormat(string pszDeviceName, out IntPtr ppFormat);
            [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, out IntPtr ppFormat);
            [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
            [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
            [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, out IntPtr pmftDefaultPeriod, out IntPtr pmftMinimumPeriod);
            [PreserveSig] int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
            [PreserveSig] int GetShareMode(string pszDeviceName, out IntPtr pMode);
            [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
            [PreserveSig] int GetPropertyValue(string pszDeviceName, int bFxStore, IntPtr key, out IntPtr pv);
            [PreserveSig] int SetPropertyValue(string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
            [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, int role);
            [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
        }

        /// <summary>
        /// Disables the specified endpoint using IPolicyConfig. Returns log message.
        /// </summary>
        private static string SetEndpointVisibility(string deviceId, int isVisible)
        {
            try
            {
                var client = new PolicyConfigClient();
                
                // Try modern Windows 11 GUID first
                if (client is IPolicyConfigAlt policyConfigAlt)
                {
                    policyConfigAlt.SetEndpointVisibility(deviceId, isVisible);
                    return $"[OK] Modern IPolicyConfigAlt used for {deviceId}";
                }
                
                // Fallback to older Windows 10/7 GUID
                if (client is IPolicyConfig policyConfig)
                {
                    policyConfig.SetEndpointVisibility(deviceId, isVisible);
                    return $"[OK] Legacy IPolicyConfig used for {deviceId}";
                }

                return $"[FAIL] No valid COM interface found for PolicyConfigClient!";
            }
            catch (Exception ex)
            {
                return $"[ERROR] COM Exception on {deviceId}: {ex.Message}";
            }
        }

        public static List<AudioDeviceItem> GetAllDevices()
        {
            var list = new List<AudioDeviceItem>();
            try
            {
                var enumerator = new MMDeviceEnumerator();
                // We want to list both Active and Disabled devices so user can see them
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active | DeviceState.Disabled | DeviceState.Unplugged);
                
                foreach (var device in devices)
                {
                    list.Add(new AudioDeviceItem
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        IsPlayback = device.DataFlow == DataFlow.Render,
                        IsDisabled = device.State == DeviceState.Disabled
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllDevices Error: {ex.Message}");
            }
            return list;
        }

        public static string DisableDevice(string deviceId)
        {
            return SetEndpointVisibility(deviceId, 0); 
        }

        public static string EnableDevice(string deviceId)
        {
            return SetEndpointVisibility(deviceId, 1);
        }

        public static string EnforceDisabledDevicesWithLog(List<string> disabledDeviceIds)
        {
            if (disabledDeviceIds == null || disabledDeviceIds.Count == 0) return "Hiçbir aygıt devredışı bırakılacak olarak seçilmemiş.";
            
            var sb = new System.Text.StringBuilder();
            var all = GetAllDevices();
            int attempted = 0;
            foreach (var dev in all)
            {
                if (disabledDeviceIds.Contains(dev.Id) && !dev.IsDisabled)
                {
                    attempted++;
                    sb.AppendLine($"Devredışı bırakılıyor: {dev.Name}");
                    string log = DisableDevice(dev.Id);
                    sb.AppendLine($" -> Sonuç: {log}");
                }
            }

            if (attempted == 0)
            {
                sb.AppendLine("Tüm seçili aygıtlar zaten devredışı durumda.");
            }
            return sb.ToString();
        }

        public static void EnforceDisabledDevices(List<string> disabledDeviceIds)
        {
            EnforceDisabledDevicesWithLog(disabledDeviceIds);
        }
    }
}
