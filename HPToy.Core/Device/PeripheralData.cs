using System.Buffers.Binary;
using System.Linq;
using HPToy.Core.Ble;
using HPToy.Core.Objects;
using HPToy.Core.Tas5558;

namespace HPToy.Core.Device;

public sealed class PeripheralData
{
    private const byte BiquadParametric = 3;

    public const short PeripheralConfigLength = 0x24;
    private const short BiquadTypeOffset = 0x18;
    private const short PresetDataOffset = 0x20;

    public const byte I2cAddr = 0x34;
    public const short Version = 12;
    public const byte InitDspDelay = 32;

    private byte _i2cAddr;
    private byte _successWriteFlag;
    private short _version;
    private int _pairingCode;
    private byte _initDspDelay;
    private byte _advertiseMode;
    private short _gainChannel3;
    private EnergyConfig _energyConfig = new();
    private byte[] _biquadTypes = Array.Empty<byte>();
    private byte _outputMode;
    private short _dataBufLength;
    private short _dataBytesLength;
    private List<HiFiToyDataBuf> _dataBufs = new();

    private byte[]? _importData;

    public PeripheralData(HiFiToyDevice device)
    {
        _i2cAddr = I2cAddr;
        _successWriteFlag = 0;
        _version = Version;
        _pairingCode = device.GetPairingCode();
        _initDspDelay = InitDspDelay;
        _advertiseMode = device.GetAdvertiseMode().GetMode();
        _gainChannel3 = device.GetOutputMode().GetGainCh3();
        _energyConfig = device.GetEnergyConfig();
        SetBiquadTypes(device.GetActivePreset().Filters.GetBiquadTypes());
        _outputMode = device.GetOutputMode().IsUnbalance() ? (byte)1 : (byte)0;

        var dataBufs = device.GetActivePreset().GetDataBufs();
        AppendAmModeDataBuf(dataBufs, device.GetAmMode(), device.IsNewPDV21Hw());
        SetDataBufs(dataBufs);
    }

    public PeripheralData(byte[] biquadTypes, List<HiFiToyDataBuf> dataBufs)
    {
        Clear();

        var dev = HiFiToyDeviceManager.Instance.GetActiveDevice();
        SetBiquadTypes(biquadTypes);
        _outputMode = dev.GetOutputMode().IsUnbalance() ? (byte)1 : (byte)0;

        AppendAmModeDataBuf(dataBufs, dev.GetAmMode(), dev.IsNewPDV21Hw());
        SetDataBufs(dataBufs);
    }

    public PeripheralData()
    {
        Clear();
    }

    public void Clear()
    {
        _i2cAddr = I2cAddr;
        _successWriteFlag = 0;
        _version = Version;
        _pairingCode = 0;
        _initDspDelay = InitDspDelay;
        _advertiseMode = AdvertiseMode.AlwaysEnabled;

        var om = new OutputMode();
        _gainChannel3 = om.GetGainCh3();
        _energyConfig = new EnergyConfig();
        SetBiquadTypes(new byte[]
        {
            BiquadParametric, BiquadParametric, BiquadParametric, BiquadParametric,
            BiquadParametric, BiquadParametric, BiquadParametric
        });
        _outputMode = om.IsUnbalance() ? (byte)1 : (byte)0;
        _dataBufLength = 0;
        _dataBytesLength = 0;
        _dataBufs = new List<HiFiToyDataBuf>();
    }

    public short GetDataBytesLength() => _dataBytesLength;

    public short GetDataBufLength() => (short)(_dataBytesLength - PeripheralConfigLength);

    private static short CalcDataBytesLength(List<HiFiToyDataBuf> dataBufs)
    {
        short length = 0;
        foreach (var buf in dataBufs)
        {
            length += (short)(buf.GetLength() + 2);
        }
        return (short)(length + PeripheralConfigLength);
    }

    private bool SetBiquadTypes(byte[] biquadTypes)
    {
        if (biquadTypes.Length == 7)
        {
            _biquadTypes = biquadTypes;
            return true;
        }
        return false;
    }

    public byte[] GetBiquadTypes() => _biquadTypes;

    private static HiFiToyDataBuf? FindDataBufWithAddr(byte addr, List<HiFiToyDataBuf> dataBufs)
    {
        foreach (var db in dataBufs)
        {
            if (db.GetAddr() == addr) return db;
        }
        return null;
    }

    private static void RestrictBassFilterGain(List<HiFiToyDataBuf> dataBufs)
    {
        var bassFilterBuf = FindDataBufWithAddr(TAS5558.BASS_FILTER_SET_REG, dataBufs);
        if (bassFilterBuf == null) return;

        var bb = bassFilterBuf.GetData();
        if (bb.Length <= 7) return;

        var bassFilterGain = bb[7];
        if (bassFilterGain < 0x12)
        {
            bb[7] = 0x12;
            bassFilterBuf.SetData(bb);
        }
    }

    private static void AppendAmModeDataBuf(List<HiFiToyDataBuf> dataBufs, AMMode amMode, bool newHw)
    {
        if (amMode.IsEnabled())
        {
            var amBufs = amMode.GetDataBufs();
            if (amBufs.Count > 0)
            {
                dataBufs.Insert(0, amBufs[0]);
            }

            if (!newHw)
            {
                RestrictBassFilterGain(dataBufs);
            }
        }
    }

    private void SetDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        _dataBufLength = (short)dataBufs.Count;
        _dataBytesLength = CalcDataBytesLength(dataBufs);
        _dataBufs = dataBufs;
    }

    public List<HiFiToyDataBuf> GetDataBufs() => _dataBufs;

    public bool IsPresetCached() =>
        _dataBytesLength > 0 && _dataBufs.Count > 0 && _dataBufLength > 0;

    /// <summary>Connect handshake already parsed flash header (body may still be missing).</summary>
    public bool HasParsedHeader() => _dataBytesLength > 0;

    public PeripheralData Clone()
    {
        var copy = new PeripheralData();
        copy._i2cAddr = _i2cAddr;
        copy._successWriteFlag = _successWriteFlag;
        copy._version = _version;
        copy._pairingCode = _pairingCode;
        copy._initDspDelay = _initDspDelay;
        copy._advertiseMode = _advertiseMode;
        copy._gainChannel3 = _gainChannel3;
        copy._energyConfig.ParseBinary(_energyConfig.GetBinary().AsSpan());
        copy._biquadTypes = (byte[])_biquadTypes.Clone();
        copy._outputMode = _outputMode;
        copy._dataBufLength = _dataBufLength;
        copy._dataBytesLength = _dataBytesLength;
        copy._dataBufs = _dataBufs.Select(db => new HiFiToyDataBuf(db.GetBinary())).ToList();
        return copy;
    }

    private byte[] GetBinary()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_i2cAddr);
        writer.Write(_successWriteFlag);
        writer.Write(_version);
        writer.Write(_pairingCode);
        writer.Write(_initDspDelay);
        writer.Write(_advertiseMode);
        writer.Write(_gainChannel3);
        writer.Write(_energyConfig.GetBinary());
        writer.Write(_biquadTypes);
        writer.Write(_outputMode);
        writer.Write(_dataBufLength);
        writer.Write(_dataBytesLength);

        foreach (var db in _dataBufs)
        {
            writer.Write(db.GetBinary());
        }

        return ms.ToArray();
    }

    private byte[] GetBiquadTypeBinary()
    {
        var data = GetBinary();
        return BinaryOperation.CopyOfRange(data, BiquadTypeOffset, BiquadTypeOffset + 7);
    }

    private byte[] GetPresetBinary()
    {
        var data = GetBinary();
        return BinaryOperation.CopyOfRange(data, PresetDataOffset, data.Length);
    }

    public void Export() => Export(null);

    public void Export(IPostProcess? postProcess)
    {
        if (!BleClient.Instance.IsConnected) return;
        BleClient.Instance.EnqueueFlashExport(postProcess, WriteExportPackets);
    }

    internal void WriteExportPackets()
    {
        BleClient.Instance.SendBufToDsp(0, GetBinary());
        BleClient.Instance.SendWriteFlag(1);
        BleClient.Instance.SetInitDsp();
    }

    public void ExportPreset(IPostProcess? postProcess, bool notifyIfRejected = true)
    {
        if (!BleClient.Instance.IsConnected) return;
        BleClient.Instance.EnqueueFlashExport(postProcess, WriteExportPresetPackets, notifyIfRejected);
    }

    /// <summary>Obsolete — AM store uses <see cref="ExportPreset"/> (full preset flash write).</summary>
    internal void WriteAmModeFlashPackets(byte[] amBinary)
    {
        BleClient.Instance.SendWriteFlag(0);
        BleClient.Instance.SendBufToDsp(PresetDataOffset, amBinary);
        BleClient.Instance.SendWriteFlag(1);
        BleClient.Instance.SetInitDsp();
    }

    internal void WriteExportPresetPackets() => WritePresetToFlash(withInitDsp: true);

    internal void WritePresetToFlash(bool withInitDsp)
    {
        BleClient.Instance.SendWriteFlag(0);
        BleClient.Instance.SendBufToDsp(BiquadTypeOffset, GetBiquadTypeBinary());
        BleClient.Instance.SendBufToDsp(PresetDataOffset, GetPresetBinary());
        BleClient.Instance.SendWriteFlag(1);
        if (withInitDsp)
            BleClient.Instance.SetInitDsp();
    }

    /// <summary>
    /// Copies peripheral header fields read from the device into the in-memory
    /// <see cref="HiFiToyDevice"/> model. Without this, Options (clip/auto-off) and
    /// push-to-device use stale local defaults and wipe hardware settings.
    /// </summary>
    public void ApplyHeaderToDevice(HiFiToyDevice device)
    {
        device.GetEnergyConfig().ParseBinary(_energyConfig.GetBinary().AsSpan());
        device.GetAdvertiseMode().SetMode(_advertiseMode);

        var outputMode = device.GetOutputMode();
        if (_outputMode == 0)
            outputMode.SetValue(OutputMode.BalanceOutMode);
        else if (_gainChannel3 == 16384)
            outputMode.SetValue(OutputMode.UnbalanceBoostOutMode);
        else
            outputMode.SetValue(OutputMode.UnbalanceOutMode);

        device.SetPairingCode(_pairingCode);
    }

    private bool ParseHeader(byte[] data)
    {
        if (data.Length < PeripheralConfigLength) return false;

        var offset = 0;
        _i2cAddr = data[offset++];
        _successWriteFlag = data[offset++];
        _version = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        offset += 2;
        _pairingCode = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        _initDspDelay = data[offset++];
        _advertiseMode = data[offset++];
        _gainChannel3 = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        offset += 2;
        _energyConfig.ParseBinary(data.AsSpan(offset, 12));
        offset += 12;

        _biquadTypes = new byte[7];
        Array.Copy(data, offset, _biquadTypes, 0, 7);
        offset += 7;

        _outputMode = data[offset++];
        _dataBufLength = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        offset += 2;
        _dataBytesLength = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        return true;
    }

    public void ImportHeader(IPostProcess? postProcess)
    {
        if (!BleClient.Instance.IsConnected) return;

        void OnParamData(byte[] data)
        {
            _importData = BinaryOperation.ConcatData(_importData ?? Array.Empty<byte>(), data);

            if (_importData.Length == 40)
            {
                BleClient.Instance.UnsubscribeParamData(OnParamData);
                if (!ParseHeader(_importData))
                {
                    _dataBytesLength = -1;
                }
                postProcess?.OnPostProcess();
            }
            else
            {
                BleClient.Instance.GetDspDataWithOffset(20);
            }
        }

        BleClient.Instance.SubscribeParamData(OnParamData);
        _importData = Array.Empty<byte>();
        BleClient.Instance.GetDspDataWithOffset(0);
    }

    /// <summary>Reads preset body bytes after <see cref="ImportHeader"/>.</summary>
    public void ImportPresetBody(IPostProcess? postProcess)
    {
        if (!BleClient.Instance.IsConnected) return;

        if (_dataBytesLength == -1)
        {
            postProcess?.OnPostProcess();
            return;
        }

        void OnParamData(byte[] data)
        {
            _importData = BinaryOperation.ConcatData(_importData ?? Array.Empty<byte>(), data);

            if (!IsPresetBodyReady(_importData))
            {
                var offset = (short)(PeripheralConfigLength + _importData.Length);
                BleClient.Instance.GetDspDataWithOffset(offset);
                return;
            }

            BleClient.Instance.UnsubscribeParamData(OnParamData);
            HandleImportData(_importData);
            postProcess?.OnPostProcess();
        }

        BleClient.Instance.SubscribeParamData(OnParamData);
        _importData = Array.Empty<byte>();
        BleClient.Instance.GetDspDataWithOffset(PeripheralConfigLength);
    }

    private bool IsPresetBodyReady(byte[] body) =>
        body.Length >= GetDataBufLength() &&
        CountParsedBuffers(body, _dataBufLength) >= _dataBufLength;

    private static int CountParsedBuffers(byte[] importData, int maxBuffers)
    {
        var offset = 0;
        var count = 0;

        for (var i = 0; i < maxBuffers; i++)
        {
            if (offset + 2 > importData.Length)
                break;

            var lengthByte = importData[offset + 1];
            var available = importData.Length - offset - 2;
            if (available < 0)
                break;

            var dataLen = lengthByte <= available ? lengthByte : (byte)available;
            offset += 2 + dataLen;
            count++;
        }

        return count;
    }

    public void Import(IPostProcess? postProcess)
    {
        if (!BleClient.Instance.IsConnected) return;

        ImportHeader(() =>
        {
            if (_dataBytesLength == -1)
            {
                postProcess?.OnPostProcess();
                return;
            }

            void OnParamData(byte[] data)
            {
                _importData = BinaryOperation.ConcatData(_importData ?? Array.Empty<byte>(), data);

                if (!IsPresetBodyReady(_importData))
                {
                    var offset = (short)(PeripheralConfigLength + _importData.Length);
                    BleClient.Instance.GetDspDataWithOffset(offset);
                    return;
                }

                BleClient.Instance.UnsubscribeParamData(OnParamData);
                HandleImportData(_importData);
                postProcess?.OnPostProcess();
            }

            BleClient.Instance.SubscribeParamData(OnParamData);
            _importData = Array.Empty<byte>();
            BleClient.Instance.GetDspDataWithOffset(PeripheralConfigLength);
        });
    }

    private void ImportHeader(Action onComplete)
    {
        if (!BleClient.Instance.IsConnected) return;

        void OnParamData(byte[] data)
        {
            _importData = BinaryOperation.ConcatData(_importData ?? Array.Empty<byte>(), data);

            if (_importData.Length == 40)
            {
                BleClient.Instance.UnsubscribeParamData(OnParamData);
                if (!ParseHeader(_importData))
                {
                    _dataBytesLength = -1;
                }
                onComplete();
            }
            else
            {
                BleClient.Instance.GetDspDataWithOffset(20);
            }
        }

        BleClient.Instance.SubscribeParamData(OnParamData);
        _importData = Array.Empty<byte>();
        BleClient.Instance.GetDspDataWithOffset(0);
    }

    private void HandleImportData(byte[] importData)
    {
        _dataBufs = new List<HiFiToyDataBuf>();
        var offset = 0;

        for (var i = 0; i < _dataBufLength; i++)
        {
            if (offset + 2 > importData.Length)
                break;

            var addr = importData[offset];
            var length = importData[offset + 1];
            var available = importData.Length - offset - 2;
            if (available < 0)
                break;

            if (length > available)
                length = (byte)available;

            var data = new byte[length];
            if (length > 0)
                Array.Copy(importData, offset + 2, data, 0, length);

            _dataBufs.Add(new HiFiToyDataBuf(addr, data));
            offset += 2 + length;
        }
    }
}
