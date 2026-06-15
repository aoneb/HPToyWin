using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using HPToy.Core.Device;

namespace HPToy.Core.Ble;

public sealed class BleScanner : IDisposable
{
    public static readonly string[] SupportedNames = ["HPToyPeripheral", "PDV21Peripheral"];

    private const string BleAssociationAqs =
        "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

    private static readonly string[] WatcherProperties =
    [
        "System.Devices.Aep.DeviceAddress",
        "System.ItemNameDisplay",
        "System.Devices.Aep.Bluetooth.Le.IsConnectable"
    ];

    private BluetoothLEAdvertisementWatcher? _advWatcher;
    private DeviceWatcher? _deviceWatcher;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _advNamesByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _deviceIdsByAddress = new(StringComparer.OrdinalIgnoreCase);

    public event Action<BleDeviceInfo>? DeviceFound;
    public event Action<string>? ScanStatus;

    public void Start()
    {
        Stop();
        _seen.Clear();
        _advNamesByAddress.Clear();
        _deviceIdsByAddress.Clear();

        _ = PrepareScanEnvironmentAsync();
        StartAdvertisementWatcher();
        StartDeviceWatcher();
        ScanStatus?.Invoke("正在扫描…");
    }

    private async Task PrepareScanEnvironmentAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                ScanStatus?.Invoke("未检测到蓝牙适配器。请在 Windows 设置中打开蓝牙。");
                return;
            }

            if (!adapter.IsLowEnergySupported)
            {
                ScanStatus?.Invoke("本机蓝牙不支持 BLE。");
                return;
            }
        }
        catch (Exception ex)
        {
            ScanStatus?.Invoke($"蓝牙检查失败: {ex.Message}");
        }

        await EnumerateSavedDevicesAsync();
    }

    private void StartAdvertisementWatcher()
    {
        try
        {
            _advWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            _advWatcher.Received += OnAdvertisementReceived;
            _advWatcher.Stopped += OnAdvertisementWatcherStopped;
            _advWatcher.Start();
        }
        catch (Exception ex)
        {
            ScanStatus?.Invoke($"启动广播扫描失败: {ex.Message}");
        }
    }

    private void StartDeviceWatcher()
    {
        try
        {
            _deviceWatcher = DeviceInformation.CreateWatcher(
                BleAssociationAqs,
                WatcherProperties,
                DeviceInformationKind.AssociationEndpoint);
            _deviceWatcher.Added += OnDeviceWatcherAdded;
            _deviceWatcher.Updated += OnDeviceWatcherUpdated;
            _deviceWatcher.EnumerationCompleted += OnDeviceWatcherEnumerationCompleted;
            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            ScanStatus?.Invoke($"启动设备扫描失败: {ex.Message}");
        }
    }

    private async Task EnumerateSavedDevicesAsync()
    {
        foreach (var device in HiFiToyDeviceManager.Instance.GetDevices())
        {
            var mac = device.GetMac();
            if (string.IsNullOrWhiteSpace(mac) || mac.Equals("demo", StringComparison.OrdinalIgnoreCase))
                continue;

            var address = ParseMac(mac);
            if (address == 0)
                continue;

            await TryReportFromAddressAsync(address, device.GetName());
        }
    }

    private void OnDeviceWatcherAdded(DeviceWatcher w, DeviceInformation device)
    {
        _ = HandleWatcherDeviceAsync(device.Id, device.Name);
    }

    private void OnDeviceWatcherUpdated(DeviceWatcher w, DeviceInformationUpdate update)
    {
        _ = HandleWatcherDeviceAsync(update.Id, null);
    }

    private async Task HandleWatcherDeviceAsync(string deviceId, string? name)
    {
        try
        {
            var ble = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (ble == null)
                return;

            var resolvedName = ResolveName(name, ble);
            if (string.IsNullOrEmpty(resolvedName))
            {
                var key = ble.BluetoothAddress.ToString("X12");
                resolvedName = _advNamesByAddress.GetValueOrDefault(key) ?? "";
            }

            if (!IsHptoyCandidate(resolvedName, ble.BluetoothAddress))
                return;

            var addrKey = ble.BluetoothAddress.ToString("X12");
            _deviceIdsByAddress[addrKey] = deviceId;
            ReportDevice(ble.BluetoothAddress, NormalizePeripheralName(
                string.IsNullOrEmpty(resolvedName) ? _advNamesByAddress.GetValueOrDefault(addrKey) : resolvedName),
                deviceId);
        }
        catch
        {
            // Device may be out of range.
        }
    }

    private async Task TryReportFromAddressAsync(ulong address, string? fallbackName)
    {
        try
        {
            var ble = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (ble == null)
            {
                ReportDevice(address, NormalizePeripheralName(fallbackName));
                return;
            }

            var name = ResolveName(fallbackName, ble);
            if (string.IsNullOrEmpty(name))
                name = _advNamesByAddress.GetValueOrDefault(address.ToString("X12")) ?? "";

            if (!IsHptoyCandidate(name, ble.BluetoothAddress))
                return;

            ReportDevice(ble.BluetoothAddress, NormalizePeripheralName(name));
        }
        catch
        {
            ReportDevice(address, NormalizePeripheralName(fallbackName));
        }
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher w,
        BluetoothLEAdvertisementReceivedEventArgs e)
    {
        var name = e.Advertisement.LocalName;
        if (string.IsNullOrEmpty(name))
        {
            foreach (var section in e.Advertisement.DataSections)
            {
                if (section.DataType == 0x09 && section.Data.Length > 0)
                {
                    var bytes = section.Data.ToArray();
                    name = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    break;
                }
            }
        }

        if (!IsSupportedName(name))
            return;

        var key = e.BluetoothAddress.ToString("X12");
        _advNamesByAddress[key] = name!;
        ReportDevice(e.BluetoothAddress, name!, _deviceIdsByAddress.GetValueOrDefault(key));
    }

    private void OnAdvertisementWatcherStopped(
        BluetoothLEAdvertisementWatcher w,
        BluetoothLEAdvertisementWatcherStoppedEventArgs e)
    {
        if (e.Error != BluetoothError.Success)
            ScanStatus?.Invoke($"BLE 广播扫描停止: {e.Error}");
    }

    private void OnDeviceWatcherEnumerationCompleted(DeviceWatcher w, object e)
    {
        if (_seen.Count == 0)
            ScanStatus?.Invoke(
                "未发现 PowerDAC。请确认设备已通电，且 Windows 蓝牙已开启。");
    }

    private static string ResolveName(string? fallbackName, BluetoothLEDevice ble)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName;

        var name = ble.DeviceInformation.Name;
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        return ble.Name ?? "";
    }

    private bool IsHptoyCandidate(string? name, ulong address)
    {
        if (IsSupportedName(name))
            return true;

        return _advNamesByAddress.ContainsKey(address.ToString("X12"));
    }

    private static bool IsSupportedName(string? name) =>
        !string.IsNullOrEmpty(name) && SupportedNames.Contains(name);

    private static string NormalizePeripheralName(string? name)
    {
        if (IsSupportedName(name))
            return name!;

        return "PDV21Peripheral";
    }

    public void Stop()
    {
        if (_advWatcher != null)
        {
            _advWatcher.Received -= OnAdvertisementReceived;
            _advWatcher.Stopped -= OnAdvertisementWatcherStopped;
            _advWatcher.Stop();
            _advWatcher = null;
        }

        if (_deviceWatcher != null)
        {
            _deviceWatcher.Added -= OnDeviceWatcherAdded;
            _deviceWatcher.Updated -= OnDeviceWatcherUpdated;
            _deviceWatcher.EnumerationCompleted -= OnDeviceWatcherEnumerationCompleted;
            _deviceWatcher.Stop();
            _deviceWatcher = null;
        }
    }

    private void ReportDevice(ulong address, string name, string? deviceId = null)
    {
        var id = address.ToString("X12");
        if (_seen.Contains(id))
            return;

        _seen.Add(id);
        if (!string.IsNullOrEmpty(deviceId))
            _deviceIdsByAddress[id] = deviceId;

        var resolvedId = deviceId ?? _deviceIdsByAddress.GetValueOrDefault(id);
        DeviceFound?.Invoke(new BleDeviceInfo(address, name, BleClient.FormatMac(address), resolvedId));
    }

    public static ulong ParseMac(string mac)
    {
        var parts = mac.Split(':', '-');
        if (parts.Length != 6)
            return 0;

        ulong address = 0;
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return 0;
            address |= (ulong)b << ((5 - i) * 8);
        }

        return address;
    }

    public void Dispose() => Stop();
}

public sealed record BleDeviceInfo(ulong Address, string Name, string DisplayAddress, string? DeviceId = null);
