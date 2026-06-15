using System.Text.Json;
using System.Text.Json.Serialization;

namespace HPToy.Core.Device;

public sealed class HiFiToyDeviceManager
{
    private static HiFiToyDeviceManager? _instance;
    private readonly Dictionary<string, HiFiToyDevice> _deviceMap = new();
    private string _activeDeviceKey = "demo";

    public static HiFiToyDeviceManager Instance => _instance ??= new HiFiToyDeviceManager();

    private static string MapFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HPToy",
            "HiFiToyDeviceMap.json");

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class DeviceMapFile
    {
        public string? ActiveDeviceKey { get; set; }
        public Dictionary<string, HiFiToyDevice>? Devices { get; set; }
    }

    private HiFiToyDeviceManager()
    {
        Restore();
        if (GetDevice("demo") == null)
        {
            SetDevice("demo", new HiFiToyDevice());
        }
    }

    public void SetActiveDeviceKey(string key) => _activeDeviceKey = key;

    public void SetActiveDevice(HiFiToyDevice device)
    {
        var key = device.GetMac();
        SetActiveDeviceKey(key);
        SetDevice(key, device);
    }

    public HiFiToyDevice GetActiveDevice()
    {
        if (_deviceMap.TryGetValue(_activeDeviceKey, out var device))
            return device;

        if (_activeDeviceKey != "demo" && _deviceMap.TryGetValue("demo", out var demo))
            return demo;

        return new HiFiToyDevice();
    }

    public void SetDevice(string key, HiFiToyDevice device)
    {
        ApplyDeviceFlags(device);
        _deviceMap[key] = device;
        Store();
    }

    public HiFiToyDevice? GetDevice(string key) =>
        _deviceMap.TryGetValue(key, out var device) ? device : null;

    /// <summary>Find persisted device by map key or stored MAC (scan address format may differ).</summary>
    public HiFiToyDevice? FindDeviceByMac(string mac)
    {
        if (TryGetDevice(mac, out var direct))
            return direct;

        foreach (var entry in _deviceMap)
        {
            if (string.Equals(entry.Key, mac, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
            if (string.Equals(entry.Value.GetMac(), mac, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return null;
    }

    private bool TryGetDevice(string key, out HiFiToyDevice? device) =>
        _deviceMap.TryGetValue(key, out device);

    public ICollection<HiFiToyDevice> GetDevices() => _deviceMap.Values;

    public void Description()
    {
        foreach (var entry in _deviceMap)
        {
            entry.Value.Description();
        }
    }

    public void Restore()
    {
        try
        {
            if (!File.Exists(MapFilePath))
            {
                _deviceMap.Clear();
                _activeDeviceKey = "demo";
                SetDevice("demo", new HiFiToyDevice());
                return;
            }

            var json = File.ReadAllText(MapFilePath);
            var wrapped = JsonSerializer.Deserialize<DeviceMapFile>(json, JsonOptions);
            if (wrapped?.Devices != null)
            {
                _deviceMap.Clear();
                foreach (var entry in wrapped.Devices)
                {
                    _deviceMap[entry.Key] = entry.Value;
                    ApplyDeviceFlags(entry.Value);
                }

                if (!string.IsNullOrEmpty(wrapped.ActiveDeviceKey) &&
                    _deviceMap.ContainsKey(wrapped.ActiveDeviceKey))
                    _activeDeviceKey = wrapped.ActiveDeviceKey;
                return;
            }

            var restored = JsonSerializer.Deserialize<Dictionary<string, HiFiToyDevice>>(json, JsonOptions);
            if (restored == null)
            {
                _deviceMap.Clear();
                _activeDeviceKey = "demo";
                SetDevice("demo", new HiFiToyDevice());
                return;
            }

            _deviceMap.Clear();
            foreach (var entry in restored)
            {
                _deviceMap[entry.Key] = entry.Value;
                ApplyDeviceFlags(entry.Value);
            }
        }
        catch (Exception)
        {
            _deviceMap.Clear();
            _activeDeviceKey = "demo";
            SetDevice("demo", new HiFiToyDevice());
        }
    }

    public static void ApplyDeviceFlags(HiFiToyDevice device)
    {
        if (device.GetName().Equals("PDV21Peripheral", StringComparison.Ordinal))
        {
            device.SetNewPDV21Hw(true);
            device.GetOutputMode().SetHwSupported(true);
        }
    }

    public void Store()
    {
        try
        {
            var dir = Path.GetDirectoryName(MapFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = new DeviceMapFile
            {
                ActiveDeviceKey = _activeDeviceKey,
                Devices = _deviceMap
            };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(MapFilePath, json);
            Description();
        }
        catch (Exception)
        {
        }
    }
}
