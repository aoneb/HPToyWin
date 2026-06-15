using System.Text.Json.Serialization;
using HPToy.Core.Ble;

namespace HPToy.Core.Device;

public sealed class AdvertiseMode
{
    public const byte AlwaysEnabled = 0;
    public const byte After1MinDisabled = 1;

    [JsonInclude] private byte _mode;

    public AdvertiseMode()
    {
        SetDefault();
    }

    public void SetDefault()
    {
        SetMode(AlwaysEnabled);
    }

    public void SetMode(byte mode)
    {
        if (mode < AlwaysEnabled) mode = AlwaysEnabled;
        if (mode > After1MinDisabled) mode = After1MinDisabled;
        _mode = mode;
    }

    public void SetModeWithWriteToDsp(byte mode)
    {
        SetMode(mode);
        SendToDsp();
    }

    public byte GetMode() => _mode;

    public void SendToDsp()
    {
        var d = new byte[] { CommonCommand.SetAdvertiseMode, _mode, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }

    public void ReadFromDsp()
    {
        var d = new byte[] { CommonCommand.GetAdvertiseMode, 0, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }
}
