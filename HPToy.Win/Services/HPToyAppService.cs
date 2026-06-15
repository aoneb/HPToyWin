using System.Windows;
using System.Windows.Threading;
using HPToy.Core.Ble;
using HPToy.Core.Device;
using HPToy.Core.Objects;

namespace HPToy.Win.Services;

public sealed class HPToyAppService
{
    public static HPToyAppService Instance { get; } = new();

    public event Action? DeviceChanged;
    public event Action? ConnectionStateChanged;
    public event Action<string>? LogMessage;
    public event Action? PairingRequired;
    public event Action<short, short>? PresetChecksumMismatch;
    public event Action<int>? WriteProgress;
    public event Action? WriteCompleted;
    public event Action? FlashTransferRejected;

    public bool IsFlashTransferActive => BleClient.Instance.IsFlashTransferActive;

    private HPToyAppService()
    {
        BleClient.Instance.PostToUi = PostToUi;
        BleClient.Instance.StateChanged += _ => ConnectionStateChanged?.Invoke();
        BleClient.Instance.Log += msg => LogMessage?.Invoke(msg);
        BleClient.Instance.PairingRequired += () => PairingRequired?.Invoke();
        BleClient.Instance.PresetChecksumMismatch += (d, a) => PresetChecksumMismatch?.Invoke(d, a);
        BleClient.Instance.WriteProgress += count => WriteProgress?.Invoke(count);
        BleClient.Instance.WriteCompleted += () => WriteCompleted?.Invoke();
        BleClient.Instance.DeviceSettingsImported += () => DeviceChanged?.Invoke();
        BleClient.Instance.ClipFlagChanged += _ => DeviceChanged?.Invoke();
        BleClient.Instance.FlashTransferRejected += () => FlashTransferRejected?.Invoke();
    }

    /// <summary>BLE callbacks arrive off the UI thread; never touch WPF from there.</summary>
    public static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    private static void PostToUi(Action action) => RunOnUi(action);

    public void Initialize(string officialPresetsPath)
    {
        HiFiToyPresetManager.Instance.Initialize(officialPresetsPath);
        HiFiToyDeviceManager.Instance.Restore();
    }

    public HiFiToyDevice Device => HiFiToyDeviceManager.Instance.GetActiveDevice();

    public BleConnectionState ConnectionState => BleClient.Instance.State;

    public bool IsConnectionReady => BleClient.Instance.IsConnectionReady;

    public async Task ConnectAsync(BleDeviceInfo info)
    {
        var mac = BleClient.FormatMac(info.Address);
        var dev = HiFiToyDeviceManager.Instance.FindDeviceByMac(mac);
        if (dev == null)
        {
            dev = new HiFiToyDevice();
            dev.SetMac(mac);
        }

        dev.SetName(info.Name);
        dev.SetNewPDV21Hw(info.Name.Equals("PDV21Peripheral", StringComparison.Ordinal));
        if (dev.IsNewPDV21Hw())
            dev.GetOutputMode().SetHwSupported(true);
        HiFiToyDeviceManager.Instance.SetActiveDevice(dev);

        await BleClient.Instance.ConnectAsync(info.Address, dev, info.DeviceId);
        DeviceChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        await BleClient.Instance.DisconnectAsync();
        DeviceChanged?.Invoke();
    }

    public void SubmitPairingCode(int code)
    {
        Device.SetPairingCode(code);
        HiFiToyDeviceManager.Instance.Store();
        BleClient.Instance.StartPairedProcess(code);
    }

    public void ApplyPreset(string presetName)
    {
        Device.SetActiveKeyPreset(presetName);
        DeviceChanged?.Invoke();
        if (IsConnectionReady)
            Device.GetActivePreset().StoreToPeripheral();
    }

    public void SendPresetToDevice()
    {
        if (IsConnectionReady)
            Device.GetActivePreset().StoreToPeripheral();
    }

    public void ImportPresetFromDevice()
    {
        if (!IsConnectionReady) return;
        Device.ImportPreset(new PostAction(() =>
        {
            DeviceChanged?.Invoke();
            ConnectionStateChanged?.Invoke();
        }));
    }

    public void PushActivePresetToDevice()
    {
        if (!IsConnectionReady) return;
        var expected = Device.GetActivePreset().GetChecksum();
        Device.GetActivePreset().StoreToPeripheral(new PostAction(() =>
        {
            BleClient.Instance.QueryDeviceChecksum(deviceCs =>
            {
                if (deviceCs == expected)
                {
                    Device.SetAckDeviceChecksum(deviceCs);
                    LogMessage?.Invoke($"推送成功，设备校验=0x{deviceCs:X4}");
                }
                else
                    LogMessage?.Invoke(
                        $"推送后校验仍不一致：设备=0x{deviceCs:X4}，应用=0x{expected:X4}");
            });
        }));
    }

    public void NotifyDeviceChanged() => DeviceChanged?.Invoke();
}
