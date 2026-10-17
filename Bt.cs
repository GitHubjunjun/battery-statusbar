using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatusBarCs
{
    // 蓝牙电量：三层来源
    //  1. 系统属性 System.Devices.BatteryLife（AEP 层，多数设备为空）
    //  2. PnP 层 HFP 电池属性 {104EA319-6EE2-4701-BD47-8DDBF425BBE5},2
    //     （耳机类设备实际挂在这里，即 Windows 设置页显示的数据源）
    //  3. BLE 的 GATT Battery Service (0x180F / 0x2A19)
    static class Bt
    {
        const string PROP_BATTERY = "System.Devices.BatteryLife";
        const string PROP_HFP = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    
        public static void DumpAll()
        {
            var kinds = new[]
            {
                Windows.Devices.Enumeration.DeviceInformationKind.AssociationEndpoint,
                Windows.Devices.Enumeration.DeviceInformationKind.AssociationEndpointContainer,
                Windows.Devices.Enumeration.DeviceInformationKind.Device,
                Windows.Devices.Enumeration.DeviceInformationKind.DeviceContainer,
            };
            foreach (var kind in kinds)
            {
                try
                {
                    var devs = Windows.Devices.Enumeration.DeviceInformation
                        .FindAllAsync("", null, kind).AsTask().Result;
                    int n = devs == null ? 0 : devs.Count;
                    Log.W($"dump kind={kind} count={n}");
                    if (devs == null) continue;
                    foreach (var d in devs)
                    {
                        string nm = d.Name ?? "";
                        if (nm.IndexOf("FreeBuds", StringComparison.OrdinalIgnoreCase) < 0 &&
                            nm.IndexOf("蓝牙", StringComparison.OrdinalIgnoreCase) < 0 &&
                            nm.IndexOf("HUAWEI", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Log.W($"=== kind={kind} name={nm} id={d.Id}");
                        foreach (var kv in d.Properties)
                            Log.W($"    {kv.Key} = {kv.Value}");
                    }
                }
                catch (Exception ex) { Log.W($"dump err kind={kind}: {ex.Message}"); }
            }
        }

    public static List<Item> Scan()
        {
            var res = new List<Item>();
            string[] props = { PROP_BATTERY, "System.Devices.BatteryPlusCharging", "System.Devices.Aep.IsConnected" };
            try
            {
                foreach (string aqs in Selectors())
                {
                    try
                    {
                        var found = Windows.Devices.Enumeration.DeviceInformation
                            .FindAllAsync(aqs, props).AsTask().Result;
                        int n = found == null ? 0 : found.Count;
                        Log.W($"bt aqs={aqs.Substring(0, Math.Min(38, aqs.Length))} count={n}");
                        if (found == null) continue;
                        foreach (var d in found)
                        {
                            bool connected = IsConnected(d);
                            int? pct = ReadProperty(d);
                            if (pct == null) pct = ReadFromBluetoothDevice(d.Id);
                            Log.W($"  bt: {d.Name} connected={connected} battery={pct}");
                            if (pct == null)
                            {
                                try { pct = ScanGatt(d.Id).Result; } catch { pct = null; }
                                Log.W($"  bt gatt: {d.Name} -> {pct}");
                            }
                            if (pct == null && !connected) continue;
                            string nm = string.IsNullOrWhiteSpace(d.Name) ? "蓝牙设备" : d.Name;
                            if ((pct.HasValue || connected) && !res.Any(x => x.Name == nm))
                                res.Add(new Item { Name = nm, Pct = pct });
                        }
                    }
                    catch (Exception ex) { Log.W("bt aqs err: " + ex.Message); }
                }
            }
            catch (Exception ex) { Log.W("bt scan err: " + ex.Message); }

            // PnP 层 HFP 电量兜底（耳机类设备的真实数据源）
            try
            {
                var pnp = ScanPnpHfp();
                foreach (var it in res)
                {
                    if (it.Pct != null || string.IsNullOrWhiteSpace(it.Name)) continue;
                    foreach (var kv in pnp)
                    {
                        if (kv.Key.StartsWith(it.Name, StringComparison.OrdinalIgnoreCase) ||
                            it.Name.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            it.Pct = kv.Value;
                            Log.W($"  bt pnp merge: {it.Name} <- {kv.Key} = {kv.Value}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.W("bt pnp merge err: " + ex.Message); }

            // 特例显示名（匹配用原名，显示用短名）
            foreach (var it in res)
                if (it.Name == "HUAWEI FreeBuds Pro 5") it.Name = "FreeBuds Pro 5";

            return res;
        }

        // kind=Device（PnP 设备层）+ 空选择器，读 HFP 电池属性
        static Dictionary<string, int> ScanPnpHfp()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] props = { "System.ItemNameDisplay", PROP_HFP, "System.Devices.Present" };
                var devs = Windows.Devices.Enumeration.DeviceInformation
                    .FindAllAsync("", props, Windows.Devices.Enumeration.DeviceInformationKind.Device)
                    .AsTask().Result;
                if (devs == null) return map;
                foreach (var d in devs)
                {
                    object hfp = null, present = null;
                    if (d.Properties != null)
                    {
                        d.Properties.TryGetValue(PROP_HFP, out hfp);
                        d.Properties.TryGetValue("System.Devices.Present", out present);
                    }
                    if (hfp == null) continue;
                    int b;
                    try { b = Convert.ToInt32(hfp); } catch { continue; }
                    if (b < 0 || b > 100) continue;
                    try { if (present != null && !Convert.ToBoolean(present)) continue; } catch { }
                    string nm = CleanBtName(d.Name);
                    if (string.IsNullOrWhiteSpace(nm)) continue;
                    if (!map.ContainsKey(nm)) map[nm] = b;
                }
                Log.W($"bt pnp hfp: {string.Join("; ", map.Select(kv => kv.Key + "=" + kv.Value))}");
            }
            catch (Exception ex) { Log.W("bt pnp err: " + ex.Message); }
            return map;
        }

        // 去掉 HFP/Headset 等协议后缀，让名称与设置页一致，便于前缀匹配
        static string CleanBtName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            string[] suffixes =
            {
                " Hands-Free AG", " Handsfree AG", " Hands-Free", " Handsfree",
                " Headset AG", " Headset", " Stereo AG", " AV AG", " A2DP AG",
            };
            foreach (string sf in suffixes)
            {
                if (s.EndsWith(sf, StringComparison.OrdinalIgnoreCase))
                    return s.Substring(0, s.Length - sf.Length).TrimEnd();
            }
            return s;
        }

        static bool IsConnected(Windows.Devices.Enumeration.DeviceInformation d)
        {
            try
            {
                if (d.Properties != null &&
                    d.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object v) && v != null)
                    return Convert.ToBoolean(v);
            }
            catch { }
            try
            {
                var dev = Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(d.Id).AsTask().Result;
                if (dev != null)
                    return dev.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected;
            }
            catch { }
            return true; // 选择器本身已按 Connected 过滤时，视为已连接
        }

        static IEnumerable<string> Selectors()
        {
            yield return Windows.Devices.Bluetooth.BluetoothDevice
                .GetDeviceSelectorFromConnectionStatus(Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected);
            yield return Windows.Devices.Bluetooth.BluetoothLEDevice
                .GetDeviceSelectorFromConnectionStatus(Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected);
            yield return Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        }

        static int? ReadFromBluetoothDevice(string id)
        {
            try
            {
                var dev = Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(id).AsTask().Result;
                if (dev == null) return null;
                var di = dev.DeviceInformation;
                if (di != null && di.Properties != null &&
                    di.Properties.TryGetValue(PROP_BATTERY, out object v) && v != null)
                {
                    int b = Convert.ToInt32(v);
                    if (b >= 0 && b <= 100) return b;
                }
            }
            catch { }
            return null;
        }

        static int? ReadProperty(Windows.Devices.Enumeration.DeviceInformation d)
        {
            try
            {
                if (d.Properties != null && d.Properties.TryGetValue(PROP_BATTERY, out object v) && v != null)
                {
                    int b = Convert.ToInt32(v);
                    if (b >= 0 && b <= 100) return b;
                }
            }
            catch { }
            return null;
        }

        static async Task<int?> ScanGatt(string id)
        {
            try
            {
                var le = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromIdAsync(id);
                Log.W($"  bt le open: id={id.Substring(0, Math.Min(40, id.Length))} le={(le == null ? "null" : "ok")}");
                if (le == null) return null;

                // 1) 标准电池服务
                var std = await le.GetGattServicesForUuidAsync(
                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceUuids.Battery);
                if (std != null && std.Services != null)
                {
                    foreach (var s in std.Services)
                    {
                        var v = await TryRead(s);
                        if (v.HasValue) return v;
                    }
                }

                // 2) 厂商自定义服务里也可能藏 0x2A19
                var all = await le.GetGattServicesAsync();
                int cnt = (all == null || all.Services == null) ? 0 : all.Services.Count;
                var names = new System.Text.StringBuilder();
                if (all != null && all.Services != null)
                    foreach (var s0 in all.Services) names.Append(s0.Uuid.ToString().Substring(0, 8)).Append(",");
                Log.W($"  bt le: {le.Name} services={cnt} uuids={names}");
                if (all != null && all.Services != null)
                {
                    foreach (var s in all.Services)
                    {
                        var v = await TryRead(s);
                        if (v.HasValue) return v;
                    }
                }
            }
            catch { }
            return null;
        }

        static async Task<int?> TryRead(Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService s)
        {
            try
            {
                var chs = await s.GetCharacteristicsForUuidAsync(
                    Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicUuids.BatteryLevel);
                if (chs == null || chs.Characteristics == null) return null;
                foreach (var c in chs.Characteristics)
                {
                    var r = await c.ReadValueAsync();
                    if (r != null &&
                        r.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success &&
                        r.Value.Length > 0)
                    {
                        using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(r.Value))
                            return reader.ReadByte();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
