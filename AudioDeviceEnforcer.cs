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

    public static class AudioDeviceEnforcer
    {
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClient { }

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

        public static void DisableDevice(string deviceId)
        {
            try
            {
                var policyConfig = (IPolicyConfig)new PolicyConfigClient();
                // 1 = Visible, 0 = Invisible/Disabled (Wait, IPolicyConfig uses INT state: 0 is visible, 1 is disabled/hidden? No, it's boolean bVisible. 0=Disabled/Hidden, 1=Visible)
                // Actually SetEndpointVisibility(deviceId, 0) hides it. Let's use 0 for false.
                policyConfig.SetEndpointVisibility(deviceId, 0); 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DisableDevice Error: {ex.Message}");
            }
        }

        public static void EnableDevice(string deviceId)
        {
            try
            {
                var policyConfig = (IPolicyConfig)new PolicyConfigClient();
                policyConfig.SetEndpointVisibility(deviceId, 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnableDevice Error: {ex.Message}");
            }
        }

        public static void EnforceDisabledDevices(List<string> disabledDeviceIds)
        {
            if (disabledDeviceIds == null || disabledDeviceIds.Count == 0) return;
            
            var all = GetAllDevices();
            foreach (var dev in all)
            {
                if (disabledDeviceIds.Contains(dev.Id) && !dev.IsDisabled)
                {
                    DisableDevice(dev.Id);
                }
            }
        }
    }
}
