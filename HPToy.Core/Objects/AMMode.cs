using System.Text.Json.Serialization;
using HPToy.Core.Ble;
using HPToy.Core.Device;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

/// <summary>
/// Minimal AM mode register holder for peripheral export/import.
/// </summary>
public sealed class AMMode : IHiFiToyObject, ICloneable
{
    [JsonInclude] private byte[] _data = new byte[4];

    public AMMode()
    {
        Reset();
    }

    public void Reset()
    {
        _data[0] = 0x00;
        _data[1] = 0x09;
        _data[2] = 0x03;
        _data[3] = 0xF2;
    }

    public byte GetData(int index)
    {
        if (index > 3) index = 3;
        if (index < 0) index = 0;
        return _data[index];
    }

    public void SetData(int index, byte d)
    {
        if (index > 3) index = 3;
        if (index < 0) index = 0;
        _data[index] = d;
    }

    public bool IsEnabled() => (_data[1] & 0x10) != 0;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _data[1] |= 0x10;
        }
        else
        {
            _data[1] &= unchecked((byte)~0x10);
        }
    }

    public byte GetAddress() => TAS5558.AM_MODE_REG;
    public string GetInfo() => "AMMode";

    /// <summary>Sends the current AM mode bytes to live DSP RAM (lost on power cycle).</summary>
    public void SendToPeripheral(bool response)
    {
        var binary = BinaryOperation.GetBinary(GetDataBufs())!;
        BleClient.Instance.SendDataToDsp(new BlePacket(binary, 20, response));
    }

    /// <summary>
    /// Persists AM mode to hardware flash by exporting the full preset (with AM buf appended),
    /// matching Android AMMode.storeToPeripheral / PeripheralData.exportPreset.
    /// </summary>
    public void StoreToPeripheral(IPostProcess? postProcess = null)
    {
        if (!BleClient.Instance.IsConnected) return;

        var device = HiFiToyDeviceManager.Instance.GetActiveDevice();
        var preset = device.GetActivePreset();
        var peripheralData = new PeripheralData(
            preset.Filters.GetBiquadTypes(),
            preset.GetDataBufs());
        peripheralData.ExportPreset(postProcess, notifyIfRejected: false);
    }

    /// <summary>
    /// Reads the live AM mode bytes from the DSP (first data buf at offset 0x24)
    /// and imports them. Mirrors the original AMMode.readFromDsp().
    /// </summary>
    public void ReadFromDsp(Action? postProcess = null)
    {
        if (!BleClient.Instance.IsConnected) return;

        void OnParamData(byte[] data)
        {
            BleClient.Instance.UnsubscribeParamData(OnParamData);
            ImportFromDataBufs(new List<HiFiToyDataBuf> { new HiFiToyDataBuf(data) });
            postProcess?.Invoke();
        }

        BleClient.Instance.SubscribeParamData(OnParamData);
        BleClient.Instance.GetDspDataWithOffset(PeripheralData.PeripheralConfigLength);
    }

    public List<HiFiToyDataBuf> GetDataBufs() =>
        new() { new HiFiToyDataBuf(GetAddress(), (byte[])_data.Clone()) };

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 4)
            {
                var src = buf.GetData();
                Array.Copy(src, _data, 4);
                return true;
            }
        }
        return false;
    }

    public XmlData ToXmlData() => new();

    public void ImportFromXml(XmlImportReader xmlParser)
    {
    }

    public object Clone()
    {
        var clone = new AMMode();
        Array.Copy(_data, clone._data, 4);
        return clone;
    }
}
