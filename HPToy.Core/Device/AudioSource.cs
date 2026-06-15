using System.Text.Json.Serialization;
using HPToy.Core.Ble;

namespace HPToy.Core.Device;

public sealed class AudioSource
{
    public const byte SpdifSource = 0;
    public const byte UsbSource = 1;
    public const byte BtSource = 2;

    [JsonInclude] private byte _source;

    public AudioSource()
    {
        SetDefault();
    }

    public void SetDefault()
    {
        SetSource(UsbSource);
    }

    public void SetSource(byte source)
    {
        if (source < SpdifSource) source = SpdifSource;
        if (source > BtSource) source = BtSource;
        _source = source;
    }

    public void SetSourceWithWriteToDsp(byte source)
    {
        SetSource(source);
        SendToDsp();
    }

    public byte GetSource() => _source;

    public void SendToDsp()
    {
        var d = new byte[] { CommonCommand.SetAudioSource, _source, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }

    public void ReadFromDsp()
    {
        var d = new byte[] { CommonCommand.GetAudioSource, 0, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }
}
