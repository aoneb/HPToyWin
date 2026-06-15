using System.Text.Json.Serialization;
using HPToy.Core.Ble;
using HPToy.Core.Objects;

namespace HPToy.Core.Device;

public sealed class HiFiToyDevice
{
    [JsonInclude] private string _mac = "demo";
    [JsonInclude] private string _name = "Demo";
    [JsonInclude] private string _activeKeyPreset = "No processing";
    [JsonInclude] private int _pairingCode;

    [JsonInclude] private AudioSource _audioSource = new();
    [JsonInclude] private EnergyConfig _energyConfig = new();
    [JsonInclude] private AdvertiseMode _advertiseMode = new();
    [JsonInclude] private OutputMode _outputMode = new();
    [JsonInclude] private AMMode _amMode = new();
    [JsonInclude] private short _ackDeviceChecksum;
    [JsonInclude] private bool _newPdv21Hw;
    [JsonInclude] private bool _clipFlag;

    [JsonIgnore] private ToyPreset? _activePreset;

    public HiFiToyDevice()
    {
        _name = "Demo";
        _activeKeyPreset = "No processing";
        _pairingCode = 0;
        _audioSource = new AudioSource();
        _advertiseMode = new AdvertiseMode();
        _energyConfig = new EnergyConfig();
        _outputMode = new OutputMode();
        _amMode = new AMMode();
    }

    public string GetMac() => _mac;
    public void SetMac(string mac) => _mac = mac;

    public string GetName() => _name;
    public void SetName(string name) => _name = name;

    public int GetPairingCode() => _pairingCode;
    public void SetPairingCode(int pairingCode) => _pairingCode = pairingCode;

    public short GetVersion() => PeripheralData.Version;

    public string GetActiveKeyPreset() => _activeKeyPreset;

    public bool SetActiveKeyPreset(string key, ToyPreset? preset = null)
    {
        try
        {
            _activeKeyPreset = key;
            _activePreset = preset ?? HiFiToyPresetManager.Instance.GetPreset(key);
            HiFiToyDeviceManager.Instance.Store();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public short GetAckDeviceChecksum() => _ackDeviceChecksum;

    public void SetAckDeviceChecksum(short checksum)
    {
        _ackDeviceChecksum = checksum;
        HiFiToyDeviceManager.Instance.Store();
    }

    public void SyncAckFromDevice()
    {
        if (!BleClient.Instance.IsConnected) return;
        BleClient.Instance.QueryDeviceChecksum(SetAckDeviceChecksum);
    }

    public void ForceSetActiveKeyPreset(string key)
    {
        _activeKeyPreset = key;
        _activePreset = null;
        HiFiToyDeviceManager.Instance.Store();
    }

    public ToyPreset GetActivePreset()
    {
        if (_activePreset == null)
        {
            try
            {
                _activePreset = HiFiToyPresetManager.Instance.GetPreset(_activeKeyPreset);
            }
            catch (Exception)
            {
                if (_ackDeviceChecksum != 0 &&
                    !_activeKeyPreset.Equals("No processing", StringComparison.Ordinal))
                {
                    _activePreset = new ToyPreset(_activeKeyPreset);
                    _activePreset.SetChecksum(_ackDeviceChecksum);
                }
                else
                {
                    SetActiveKeyPreset("No processing");
                    _activePreset = HiFiToyPresetManager.Instance.GetPreset("No processing");
                }
            }
        }

        SyncActivePresetChecksumFromAck();
        return _activePreset!;
    }

    /// <summary>
    /// Imported presets store device checksum in .tpr; older files may lack it — use persisted ack.
    /// </summary>
    private void SyncActivePresetChecksumFromAck()
    {
        if (_activePreset == null || _ackDeviceChecksum == 0)
            return;

        if (_activeKeyPreset.Equals("No processing", StringComparison.Ordinal))
            return;

        if (_activePreset.GetChecksum() != _ackDeviceChecksum)
        {
            _activePreset.SetChecksum(_ackDeviceChecksum);
            try
            {
                _activePreset.Save(true);
            }
            catch
            {
            }
        }
    }

    public AudioSource GetAudioSource() => _audioSource;
    public void SetAudioSource(AudioSource audioSource) => _audioSource = audioSource;

    public EnergyConfig GetEnergyConfig() => _energyConfig;
    public void SetEnergyConfig(EnergyConfig energyConfig) => _energyConfig = energyConfig;

    public AdvertiseMode GetAdvertiseMode() => _advertiseMode;
    public void SetAdvertiseMode(AdvertiseMode advertiseMode) => _advertiseMode = advertiseMode;

    public OutputMode GetOutputMode() => _outputMode;

    public AMMode GetAmMode() => _amMode;

    public bool IsNewPDV21Hw() => _newPdv21Hw;
    public void SetNewPDV21Hw(bool newPdv21Hw) => _newPdv21Hw = newPdv21Hw;

    public bool GetClipFlag() => _clipFlag;
    public void SetClipFlag(bool clipFlag) => _clipFlag = clipFlag;

    /// <summary>
    /// Resets device options (pairing, energy, output, AM mode, etc.) without changing preset or DSP tuning.
    /// </summary>
    public void ResetSettingsToDefault()
    {
        _pairingCode = 0;
        _audioSource = new AudioSource();
        _advertiseMode = new AdvertiseMode();
        _energyConfig = new EnergyConfig();
        _outputMode = new OutputMode();
        if (_newPdv21Hw)
            _outputMode.SetHwSupported(true);
        _amMode = new AMMode();
        HiFiToyDeviceManager.Instance.Store();
    }

    /// <summary>
    /// Writes energy config (auto-off + clip thresholds) to live DSP.
    /// Matches Android AutoOffActivity — sendToDsp only, no InitDsp flash export.
    /// </summary>
    public void StoreEnergyConfigToPeripheral(IPostProcess? postProcess = null)
    {
        HiFiToyDeviceManager.Instance.Store();
        BleClient.Instance.QueueEnergyWrite(
            _energyConfig.GetBinary(),
            postProcess != null ? () => postProcess.OnPostProcess() : null);
    }

    public void PushSettingsToDevice()
    {
        _advertiseMode.SendToDsp();
        _energyConfig.SendToDsp();
        if (_outputMode.IsHwSupported())
            _outputMode.SendToDsp();
        _amMode.SendToPeripheral(true);
        _audioSource.SendToDsp();
        BleClient.Instance.SendNewPairingCode(_pairingCode);
    }

    public void RestoreFactorySettings(IPostProcess? postProcess)
    {
        ResetSettingsToDefault();
        SetActiveKeyPreset("No processing");

        var peripheralData = new PeripheralData(this);
        peripheralData.Export(postProcess);
    }

    public void ImportPreset(IPostProcess? postProcess)
    {
        BleClient.Instance.ImportPresetFromCache(pd =>
        {
            if (!TryApplyImportedPeripheralData(pd, out var error))
                return error ?? "保存或解析错误";

            if (postProcess != null)
                BleClient.Instance.NotifyUi(() => postProcess.OnPostProcess());
            return null;
        });
    }

    internal bool TryApplyImportedPeripheralData(PeripheralData pd, out string? error)
    {
        try
        {
            var deviceChecksum = BleClient.Instance.LastComparedDeviceChecksum;
            var key = GetActiveKeyPreset();
            ToyPreset importPreset;

            if (key.Equals("No processing", StringComparison.Ordinal))
            {
                importPreset = new ToyPreset("No processing");
            }
            else if (HiFiToyPresetManager.Instance.IsUserPresetExist(key))
            {
                importPreset = HiFiToyPresetManager.Instance.GetPreset(key);
            }
            else
            {
                importPreset = HiFiToyPresetManager.Instance.GetPreset(key);
            }

            if (!importPreset.TryPopulateFromPeripheralBuffers(
                    pd.GetDataBufs(), pd.GetBiquadTypes(), out var importError))
            {
                error = importError ?? "解析设备预设数据失败";
                return false;
            }

            importPreset.SetChecksum(deviceChecksum);
            _activePreset = importPreset;
            _activeKeyPreset = importPreset.GetName();

            if (HiFiToyPresetManager.Instance.IsUserPresetExist(importPreset.GetName()))
                importPreset.Save(true);

            SetAckDeviceChecksum(deviceChecksum);
            HiFiToyDeviceManager.Instance.Store();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void ImportPresetFromFile(string filePath, IPostProcess? postProcess)
    {
        try
        {
            var importPreset = ToyPreset.FromFile(filePath);
            importPreset.Save(true);
            SetActiveKeyPreset(importPreset.GetName());
            postProcess?.OnPostProcess();
        }
        catch (Exception)
        {
            postProcess?.OnPostProcess();
        }
    }

    public void Description()
    {
        // Intentionally no logging dependency in core layer.
    }
}
