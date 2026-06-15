using System.Text.Json.Serialization;
using HPToy.Core.Ble;

namespace HPToy.Core.Device;

public sealed class OutputMode
{
    private const short GainCh3Offset = 0x0A;
    private const short OutputTypeOffset = 0x1F;

    public const byte BalanceOutMode = 0;
    public const byte UnbalanceOutMode = 1;
    public const byte UnbalanceBoostOutMode = 2;

    [JsonInclude] private byte _value;
    [JsonInclude] private bool _hwSupported;

    public OutputMode()
    {
        SetDefault();
    }

    public void SetDefault()
    {
        SetValue(UnbalanceBoostOutMode);
        SetHwSupported(true);
    }

    public void SetValue(byte value)
    {
        if (value < BalanceOutMode) value = BalanceOutMode;
        if (value > UnbalanceBoostOutMode) value = UnbalanceBoostOutMode;
        _value = value;
    }

    public byte GetValue() => _value;

    public bool IsUnbalance() => _value > 0;

    public short GetGainCh3() =>
        _value == UnbalanceBoostOutMode ? (short)16384 : (short)0;

    public void SetHwSupported(bool hwSupported) => _hwSupported = hwSupported;

    public bool IsHwSupported() => _hwSupported;

    public void SendToDsp()
    {
        var d = new byte[] { CommonCommand.SetOutputMode, IsUnbalance() ? (byte)1 : (byte)0, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);

        var gain = GetGainCh3();
        var d1 = new byte[]
        {
            CommonCommand.SetTas5558Ch3Mixer,
            (byte)(gain & 0xFF),
            (byte)((gain >> 8) & 0xFF),
            0,
            0
        };
        BleClient.Instance.SendDataToDsp(d1, true);
    }

    /// <summary>
    /// Stub: Android broadcast-based multi-packet read is not used on desktop.
    /// WinUI layer should subscribe to <see cref="BleClient.ParamDataReceived"/> to complete the read.
    /// </summary>
    public void ReadFromDsp()
    {
        if (!BleClient.Instance.IsConnected) return;
        BleClient.Instance.GetDspDataWithOffset(GainCh3Offset);
    }

    /// <summary>
    /// GET_OUTPUT_MODE is only used to detect PDV2.1 vs PDV2 classic; HW returns incorrect values.
    /// </summary>
    public void IsSettingsAvailable()
    {
        var d = new byte[] { CommonCommand.GetOutputMode, 0, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }

    /// <summary>
    /// Applies output mode parsed from a 40-byte DSP param block (stub helper for WinUI).
    /// </summary>
    public void ApplyFromParamData(byte[] importData)
    {
        if (importData.Length < 40) return;

        var boost = (short)(importData[0] + (importData[1] << 8));
        var bal = importData[OutputTypeOffset - GainCh3Offset];
        _value = bal;
        if (boost != 0 && _value > BalanceOutMode)
        {
            _value = UnbalanceBoostOutMode;
        }
    }

    public override string ToString() => _value switch
    {
        BalanceOutMode => "Output mode = Balance",
        UnbalanceOutMode => "Output mode = Unbalance",
        UnbalanceBoostOutMode => "Output mode = Boost unbalance",
        _ => "Output mode = Error"
    };
}
