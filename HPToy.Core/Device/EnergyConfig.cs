using System.Buffers.Binary;
using System.Text.Json.Serialization;
using HPToy.Core.Ble;

namespace HPToy.Core.Device;

public sealed class EnergyConfig
{
    private const float MaxThresholdDb = 0.0f;
    private const float MinThresholdDb = -120.0f;
    private const float CorrectHighThresCoef = 4.8f;

    [JsonInclude] private float _highThresholdDb;
    [JsonInclude] private float _lowThresholdDb;
    [JsonInclude] private short _auxTimeout120ms;
    [JsonInclude] private short _usbTimeout120ms;

    public EnergyConfig()
    {
        SetDefault();
    }

    public void SetDefault()
    {
        _highThresholdDb = CorrectHighThresCoef;
        _lowThresholdDb = -55;
        _auxTimeout120ms = 2500;
        _usbTimeout120ms = 0;
    }

    public byte[] GetBinary()
    {
        var b = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(0), _highThresholdDb);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(4), _lowThresholdDb);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(8), _auxTimeout120ms);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(10), _usbTimeout120ms);
        return b;
    }

    public void ParseBinary(ReadOnlySpan<byte> b)
    {
        _highThresholdDb = BinaryPrimitives.ReadSingleLittleEndian(b);
        _lowThresholdDb = BinaryPrimitives.ReadSingleLittleEndian(b[4..]);
        _auxTimeout120ms = BinaryPrimitives.ReadInt16LittleEndian(b[8..]);
        _usbTimeout120ms = BinaryPrimitives.ReadInt16LittleEndian(b[10..]);
    }

    public void SetValues(byte[] data)
    {
        if (data.Length == 12)
        {
            ParseBinary(data);
        }
    }

    public void SetLowThresholdDb(float lowThresholdDb)
    {
        if (lowThresholdDb > MaxThresholdDb) lowThresholdDb = MaxThresholdDb;
        if (lowThresholdDb < MinThresholdDb) lowThresholdDb = MinThresholdDb;
        _lowThresholdDb = lowThresholdDb;
    }

    public float GetLowThresholdDb() => _lowThresholdDb;

    public void SetLowThresholdDbPercent(float percent)
    {
        if (percent > 1.0f) percent = 1.0f;
        if (percent < 0.0f) percent = 0.0f;
        _lowThresholdDb = (MaxThresholdDb - MinThresholdDb) * percent + MinThresholdDb;
    }

    public float GetLowThresholdDbPercent() =>
        (_lowThresholdDb - MinThresholdDb) / (MaxThresholdDb - MinThresholdDb);

    public void SetHighThresholdDb(float highThresholdDb)
    {
        if (highThresholdDb > MaxThresholdDb) highThresholdDb = MaxThresholdDb;
        if (highThresholdDb < MinThresholdDb) highThresholdDb = MinThresholdDb;
        _highThresholdDb = highThresholdDb + CorrectHighThresCoef;
    }

    public float GetHighThresholdDb() => _highThresholdDb - CorrectHighThresCoef;

    public void SetHighThresholdDbPercent(float percent)
    {
        if (percent > 1.0f) percent = 1.0f;
        if (percent < 0.0f) percent = 0.0f;
        SetHighThresholdDb((MaxThresholdDb - MinThresholdDb) * percent + MinThresholdDb);
    }

    public float GetHighThresholdDbPercent() =>
        (GetHighThresholdDb() - MinThresholdDb) / (MaxThresholdDb - MinThresholdDb);

    public void SendToDsp() => BleClient.Instance.SendDataToDsp(GetBinary(), true);

    public void ReadFromDsp()
    {
        var d = new byte[] { CommonCommand.GetEnergyConfig, 0, 0, 0, 0 };
        BleClient.Instance.SendDataToDsp(d, true);
    }
}
