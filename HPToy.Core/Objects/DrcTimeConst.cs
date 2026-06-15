using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class DrcTimeConst : IHiFiToyObject, ICloneable
{
    private const float MaxEnergy = 50.0f;
    private const float MinEnergy = 0.05f;
    private const float MaxAttack = 200.0f;
    private const float MinAttack = 1.0f;
    private const float MaxDecay = 10000.0f;
    private const float MinDecay = 10.0f;

    private byte _channel;
    private float _energyMs;
    private float _attackMs;
    private float _decayMs;

    public DrcTimeConst(byte channel, float energyMs, float attackMs, float decayMs)
    {
        SetChannel(channel);
        _energyMs = energyMs;
        _attackMs = attackMs;
        _decayMs = decayMs;
    }

    public DrcTimeConst(byte channel) : this(channel, 0.1f, 10.0f, 100.0f)
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not DrcTimeConst that) return false;
        return _channel == that._channel &&
               FloatUtility.IsFloatDiffLessThan(that._energyMs, _energyMs, 0.5f) &&
               FloatUtility.IsFloatDiffLessThan(that._attackMs, _attackMs, 0.5f) &&
               FloatUtility.IsFloatDiffLessThan(that._decayMs, _decayMs, 0.5f);
    }

    public override int GetHashCode() => _channel.GetHashCode();

    public object Clone() => MemberwiseClone();

    public void SetChannel(byte channel)
    {
        if (channel > DrcChannel.DRC_CH_8) channel = DrcChannel.DRC_CH_8;
        if (channel < DrcChannel.DRC_CH_1_7) channel = DrcChannel.DRC_CH_1_7;
        _channel = channel;
    }

    public byte GetChannel() => _channel;

    public void SetEnergyMS(float energyMs)
    {
        if (energyMs > MaxEnergy) energyMs = MaxEnergy;
        if (energyMs < MinEnergy) energyMs = MinEnergy;

        if (energyMs < 0.05f)
        {
            energyMs = 0.05f;
        }
        else if (energyMs < 0.1f)
        {
            energyMs = (int)(energyMs / 0.05f) * 0.05f;
        }
        else if (energyMs < 1.0f)
        {
            energyMs = (int)(energyMs / 0.1f) * 0.1f;
        }
        else if (energyMs < 10.0f)
        {
            energyMs = (int)energyMs;
        }
        else
        {
            energyMs = (int)(energyMs / 10.0f) * 10.0f;
        }

        _energyMs = energyMs;
    }

    public float GetEnergyMS() => _energyMs;

    public void SetAttackMS(float attackMs)
    {
        if (attackMs > MaxAttack) attackMs = MaxAttack;
        if (attackMs < MinAttack) attackMs = MinAttack;
        _attackMs = (int)attackMs;
    }

    public float GetAttackMS() => _attackMs;

    public void SetDecayMS(float decayMs)
    {
        if (decayMs > MaxDecay) decayMs = MaxDecay;
        if (decayMs < MinDecay) decayMs = MinDecay;
        _decayMs = (int)(decayMs / 10) * 10;
    }

    public float GetDecayMS() => _decayMs;

    public float GetEnergyPercent() =>
        (float)((Math.Log10(_energyMs) - Math.Log10(MinEnergy)) /
                (Math.Log10(MaxEnergy) - Math.Log10(MinEnergy)));

    public void SetEnergyPercent(float percent)
    {
        var e = (float)Math.Pow(10,
            percent * (Math.Log10(MaxEnergy) - Math.Log10(MinEnergy)) + Math.Log10(MinEnergy));
        SetEnergyMS(e);
    }

    public float GetAttackPercent() =>
        (float)((Math.Log10(_attackMs) - Math.Log10(MinAttack)) /
                (Math.Log10(MaxAttack) - Math.Log10(MinAttack)));

    public void SetAttackPercent(float percent)
    {
        var a = (float)Math.Pow(10,
            percent * (Math.Log10(MaxAttack) - Math.Log10(MinAttack)) + Math.Log10(MinAttack));
        SetAttackMS(a);
    }

    public float GetDecayPercent() =>
        (float)((Math.Log10(_decayMs) - Math.Log10(MinDecay)) /
                (Math.Log10(MaxDecay) - Math.Log10(MinDecay)));

    public void SetDecayPercent(float percent)
    {
        var d = (float)Math.Pow(10,
            percent * (Math.Log10(MaxDecay) - Math.Log10(MinDecay)) + Math.Log10(MinDecay));
        SetDecayMS(d);
    }

    public byte GetAddress() =>
        _channel == DrcChannel.DRC_CH_8 ? TAS5558.DRC2_ENERGY_REG : TAS5558.DRC1_ENERGY_REG;

    public string GetInfo() =>
        string.Format(CultureInfo.CurrentCulture, "Energy={0:F1} Attack={1:F1} Decay={2:F1}",
            _energyMs, _attackMs, _decayMs);

    public string GetEnergyDescription()
    {
        if (_energyMs < 0.1f)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}us", (int)(_energyMs * 1000));
        }
        if (_energyMs < 1.0f)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:F1}ms", _energyMs);
        }
        return string.Format(CultureInfo.CurrentCulture, "{0}ms", (int)_energyMs);
    }

    public string GetAttackDescription() =>
        string.Format(CultureInfo.CurrentCulture, "{0}ms", (int)_attackMs);

    public string GetDecayDescription() =>
        string.Format(CultureInfo.CurrentCulture, "{0}ms", (int)_decayMs);

    public void SendEnergyToPeripheral(bool response)
    {
        var p = new BlePacket(GetEnergyDataBuf().GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    public void SendAttackDecayToPeripheral(bool response)
    {
        var p = new BlePacket(GetAttackDecayDataBuf().GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    public void SendToPeripheral(bool response)
    {
        SendEnergyToPeripheral(response);
        SendAttackDecayToPeripheral(response);
    }

    private int TimeToInt(float timeMs) =>
        (int)(Math.Pow(Math.E, -2000.0f / timeMs / TAS5558.TAS5558_FS) * 0x800000) & 0x007FFFFF;

    private float IntToTimeMS(int time)
    {
        var t = (float)(time & 0x007FFFFF) / 0x800000;
        return (float)(-2000.0f / TAS5558.TAS5558_FS / Math.Log(t));
    }

    private HiFiToyDataBuf GetEnergyDataBuf()
    {
        var b = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(0), 0x800000 - TimeToInt(_energyMs));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), TimeToInt(_energyMs));
        return new HiFiToyDataBuf(GetAddress(), b);
    }

    private HiFiToyDataBuf GetAttackDecayDataBuf()
    {
        var b = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(0), 0x800000 - TimeToInt(_attackMs));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), TimeToInt(_attackMs));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(8), 0x800000 - TimeToInt(_decayMs));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(12), TimeToInt(_decayMs));
        return new HiFiToyDataBuf((byte)(GetAddress() + 4), b);
    }

    public List<HiFiToyDataBuf> GetDataBufs() =>
        new() { GetEnergyDataBuf(), GetAttackDecayDataBuf() };

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        var importCounter = 0;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 8)
            {
                var energy = BinaryPrimitives.ReadInt32BigEndian(buf.GetData().AsSpan(4));
                _energyMs = IntToTimeMS(energy);
                importCounter++;
            }

            if (buf.GetAddr() == GetAddress() + 4 && buf.GetLength() == 16)
            {
                var data = buf.GetData();
                var attack = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                _attackMs = IntToTimeMS(attack);
                var decay = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(12));
                _decayMs = IntToTimeMS(decay);
                importCounter++;
            }

            if (importCounter >= 2)
            {
                return true;
            }
        }

        return false;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("Energy", _energyMs);
        xmlData.AddXmlElement("Attack", _attackMs);
        xmlData.AddXmlElement("Decay", _decayMs);

        var drcTimeConstXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Channel"] = _channel.ToString(CultureInfo.InvariantCulture) };
        drcTimeConstXmlData.AddXmlElement("DrcTimeConst", xmlData, attrib);
        return drcTimeConstXmlData;
    }

    public void ImportFromXml(XmlImportReader xmlParser)
    {
        string? elementName = null;
        var count = 0;

        do
        {
            xmlParser.Next();

            if (xmlParser.EventType == XmlImportEventType.StartElement)
            {
                elementName = xmlParser.Name;
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("DrcTimeConst", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("Energy", StringComparison.Ordinal))
                {
                    _energyMs = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("Attack", StringComparison.Ordinal))
                {
                    _attackMs = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("Decay", StringComparison.Ordinal))
                {
                    _decayMs = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 3)
        {
            throw new IOException("DrcTimeConst=" + _channel + ". Import from xml is not success.");
        }
    }
}
