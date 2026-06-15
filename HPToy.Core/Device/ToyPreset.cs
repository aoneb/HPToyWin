using System.Globalization;
using HPToy.Core.Numbers;
using HPToy.Core.Objects;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;
using static HPToy.Core.Objects.DrcChannel;

namespace HPToy.Core.Device;

public sealed class ToyPreset : IHiFiToyObject, ICloneable
{
    private string _name = "No processing";
    private short _checkSum;

    public Filters Filters { get; private set; } = null!;
    public Volume MasterVolume { get; private set; } = null!;
    public BassTreble BassTreble { get; private set; } = null!;
    public Loudness Loudness { get; private set; } = null!;
    public Drc Drc { get; private set; } = null!;

    public ToyPreset()
    {
        SetDefault();
    }

    public ToyPreset(string presetName)
    {
        SetDefault();
        _name = presetName;
    }

    public static ToyPreset FromFile(string filePath)
    {
        var preset = new ToyPreset(GetPresetName(Path.GetFileName(filePath)) ?? "No processing");
        using var stream = File.OpenRead(filePath);
        preset.ImportFromXml(stream);
        return preset;
    }

    public ToyPreset(string presetName, string xmlData)
    {
        SetDefault();
        _name = presetName;
        ImportFromXml(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlData)));
    }

    public ToyPreset(string filename, Stream stream)
    {
        SetDefault();
        _name = GetPresetName(filename) ?? "No processing";
        ImportFromXml(stream);
    }

    public ToyPreset(string presetName, List<HiFiToyDataBuf> dataBufs, byte[] biquadTypes)
    {
        SetDefault();
        _name = presetName;

        if (!Filters.SetBiquadTypes(biquadTypes))
        {
            throw new IOException("Biquad type length error.");
        }

        if (!ImportFromDataBufs(dataBufs))
        {
            throw new IOException("Import from data bufs error in Preset constructor.");
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ToyPreset that) return false;
        return Equals(Filters, that.Filters) &&
               Equals(MasterVolume, that.MasterVolume) &&
               Equals(BassTreble, that.BassTreble) &&
               Equals(Loudness, that.Loudness) &&
               Equals(Drc, that.Drc);
    }

    public override int GetHashCode() =>
        HashCode.Combine(_checkSum, Filters, MasterVolume, BassTreble, Loudness, Drc);

    public object Clone()
    {
        var preset = (ToyPreset)MemberwiseClone();
        preset.Filters = (Filters)Filters.Clone();
        preset.MasterVolume = (Volume)MasterVolume.Clone();
        preset.BassTreble = (BassTreble)BassTreble.Clone();
        preset.Loudness = (Loudness)Loudness.Clone();
        preset.Drc = (Drc)Drc.Clone();
        return preset;
    }

    private List<IHiFiToyObject> GetCharacteristics() =>
        new() { Filters, MasterVolume, BassTreble, Loudness, Drc };

    private void SetDefault()
    {
        _name = "No processing";
        Filters = new Filters(TAS5558.BIQUAD_FILTER_REG, (byte)(TAS5558.BIQUAD_FILTER_REG + 7));
        MasterVolume = new Volume(TAS5558.MASTER_VOLUME_REG, 0.0f, 0.0f, Volume.HwMuteDb);
        BassTreble = new BassTreble();
        BassTreble.SetEnabledChannel(0, 1.0f);
        BassTreble.SetEnabledChannel(1, 1.0f);
        Loudness = new Loudness();

        var drcCoef17 = new DrcCoef(DRC_CH_1_7,
            new DrcCoef.DrcPoint(DrcCoef.POINT0_INPUT_DB, -120.0f),
            new DrcCoef.DrcPoint(-72.0f, -72.0f),
            new DrcCoef.DrcPoint(-24.0f, -24.0f),
            new DrcCoef.DrcPoint(DrcCoef.POINT3_INPUT_DB, -24.0f));
        var drcTimeConst17 = new DrcTimeConst(DRC_CH_1_7, 0.1f, 10.0f, 100.0f);
        Drc = new Drc(drcCoef17, drcTimeConst17);
        Drc.SetEvaluation(Drc.DrcEvaluation.POST_VOLUME_EVAL, 0);
        Drc.SetEvaluation(Drc.DrcEvaluation.POST_VOLUME_EVAL, 1);
        UpdateChecksum();
    }

    public void SetName(string name) => _name = name;
    public string GetName() => _name;
    public short GetChecksum() => _checkSum;

    public void SetChecksum(short checksum) => _checkSum = checksum;

    public void SetFilters(Filters filters) => Filters = filters;

    public Volume GetVolume() => MasterVolume;
    public BassTreble GetBassTreble() => BassTreble;
    public Loudness GetLoudness() => Loudness;
    public Drc GetDrc() => Drc;

    public void UpdateChecksum() => UpdateChecksum(GetDataBufs());

    private void UpdateChecksum(List<HiFiToyDataBuf> dataBufs)
    {
        var binary = BinaryOperation.GetBinary(dataBufs);
        if (binary != null)
        {
            _checkSum = Checksummer.Calc(binary);
        }
    }

    public void StoreToPeripheral() => StoreToPeripheral(null);

    public void StoreToPeripheral(IPostProcess? postProcess, bool notifyIfRejected = true)
    {
        var peripheralData = new PeripheralData(Filters.GetBiquadTypes(), GetDataBufs());
        peripheralData.ExportPreset(postProcess, notifyIfRejected);
    }

    public void Save(bool rewrite)
    {
        if (HiFiToyPresetManager.Instance.IsOfficialPresetExist(_name))
        {
            throw new IOException("Official preset with this name already exist! We can not rewrite it.");
        }

        if (HiFiToyPresetManager.Instance.IsUserPresetExist(_name) && !rewrite)
        {
            throw new IOException("Preset with this name already exist!");
        }

        var dir = HiFiToyPresetManager.GetUserDir();
        var file = Path.Combine(dir, _name + ".tpr");
        File.WriteAllText(file, ToXmlData().ToString());
    }

    public byte GetAddress() => 0;
    public string GetInfo() => _name;

    public void SendToPeripheral(bool response)
    {
        foreach (var o in GetCharacteristics())
        {
            o.SendToPeripheral(response);
        }
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var list = new List<HiFiToyDataBuf>();
        foreach (var o in GetCharacteristics())
        {
            list.AddRange(o.GetDataBufs());
        }
        return list;
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        return TryPopulateFromPeripheralBuffers(dataBufs, Filters.GetBiquadTypes(), out _);
    }

    /// <summary>Import preset DSP buffers read from device flash (excludes AM mode block).</summary>
    public bool TryPopulateFromPeripheralBuffers(
        List<HiFiToyDataBuf> rawBufs,
        byte[] biquadTypes,
        out string? error)
    {
        if (rawBufs == null)
        {
            error = "数据为空";
            return false;
        }

        var dataBufs = rawBufs
            .Select(b => new HiFiToyDataBuf(b.GetBinary()))
            .ToList();
        dataBufs.RemoveAll(b => b.GetAddr() == TAS5558.AM_MODE_REG);
        RemoveBiquadMirrorBuffers(dataBufs);

        if (!Filters.SetBiquadTypes(biquadTypes))
        {
            error = "Biquad 类型长度错误";
            return false;
        }

        foreach (var o in GetCharacteristics())
        {
            if (!o.ImportFromDataBufs(dataBufs))
            {
                error = $"{o.GetInfo()} 导入失败；设备数据块: {FormatBufSummary(dataBufs)}";
                return false;
            }
        }

        var presetDataBufs = GetDataBufs();
        for (var i = dataBufs.Count - 1; i >= 0; i--)
        {
            var addr = dataBufs[i].GetAddr();
            if (!presetDataBufs.Any(pdb => pdb.GetAddr() == addr))
                dataBufs.RemoveAt(i);
        }

        UpdateChecksum(dataBufs);
        error = null;
        return true;
    }

    /// <summary>Flash stores primary + mirror (addr+7) pairs; preset import only needs primaries.</summary>
    private static void RemoveBiquadMirrorBuffers(List<HiFiToyDataBuf> dataBufs)
    {
        var mirrorBase = (byte)(TAS5558.BIQUAD_FILTER_REG + 7);
        dataBufs.RemoveAll(b =>
            b.GetAddr() >= mirrorBase &&
            b.GetAddr() < mirrorBase + 7 &&
            dataBufs.Any(p => p.GetAddr() == b.GetAddr() - 7));
    }

    private static string FormatBufSummary(List<HiFiToyDataBuf> bufs) =>
        $"{bufs.Count} 块: " + string.Join(", ", bufs.Select(b => $"0x{b.GetAddr():X2}:{b.GetLength()}"));

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        foreach (var o in GetCharacteristics())
        {
            xmlData.AddXmlData(o.ToXmlData());
        }

        var presetXmlData = new XmlData();
        var attrib = new Dictionary<string, string>
        {
            ["Type"] = "HiFiToy",
            ["Version"] = "1.0",
            ["Checksum"] = string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", _checkSum)
        };
        presetXmlData.AddXmlElement("Preset", xmlData, attrib);
        return presetXmlData;
    }

    public void ImportFromXml(XmlImportReader xmlParser)
    {
        string? elementName = null;
        var count = 0;
        short? storedChecksum = null;

        do
        {
            xmlParser.Next();

            if (xmlParser.EventType == XmlImportEventType.StartElement)
            {
                elementName = xmlParser.Name;

                if (elementName.Equals("Preset", StringComparison.Ordinal))
                {
                    var type = xmlParser.GetAttributeValue(null, "Type");
                    var version = xmlParser.GetAttributeValue(null, "Version");

                    if (type == null || !type.Equals("HiFiToy", StringComparison.Ordinal) ||
                        version == null || !version.Equals("1.0", StringComparison.Ordinal))
                    {
                        throw new IOException("Preset xml file is not correct. See \"Type\" or \"Version\" fields.");
                    }

                    storedChecksum = TryParseChecksumAttribute(
                        xmlParser.GetAttributeValue(null, "Checksum"));
                }
                else
                {
                    var addrStr = xmlParser.GetAttributeValue(null, "Address");
                    if (addrStr == null) continue;
                    var addr = ByteUtility.Parse(addrStr);

                    foreach (var o in GetCharacteristics())
                    {
                        if (o.GetAddress() == addr)
                        {
                            o.ImportFromXml(xmlParser);
                            count++;
                        }
                    }
                }
            }

            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("Preset", StringComparison.Ordinal)) break;
                elementName = null;
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count == GetCharacteristics().Count)
        {
            if (storedChecksum.HasValue)
                SetChecksum(storedChecksum.Value);
            else
                UpdateChecksum();
        }
        else
        {
            throw new IOException($"Preset {_name} from xml is parsing unsuccessfully.");
        }
    }

    private static short? TryParseChecksumAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];

        if (ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            return (short)hex;

        if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
            return num;

        return null;
    }

    private void ImportFromXml(Stream stream)
    {
        using var reader = new XmlImportReader(stream);
        ImportFromXml(reader);
    }

    private static string? GetPresetName(string? filename)
    {
        if (filename == null) return null;
        var end = filename.IndexOf(".tpr", StringComparison.OrdinalIgnoreCase);
        return end != -1 ? filename[..end] : filename;
    }
}
