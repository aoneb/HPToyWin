using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;
using static HPToy.Core.Objects.Biquad.BiquadParam.Type;

namespace HPToy.Core.Objects;

public sealed class Loudness : IHiFiToyObject, ICloneable
{
    private Biquad _biquad;
    private float _lg;
    private float _lo;
    private float _gain;
    private float _offset;

    public Loudness()
    {
        _lg = -0.5f;
        _lo = 0.0f;
        _gain = 0.0f;
        _offset = 0.0f;

        _biquad = new Biquad(TAS5558.LOUDNESS_BIQUAD_REG);
        var p = _biquad.GetParams();
        p.SetTypeValue(BIQUAD_BANDPASS);
        p.SetBorderFreq(200, 30);
        p.SetFreq(60);
        p.SetQFac(0.0f);
        p.SetDbVolume(0.0f);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Loudness loudness) return false;
        return FloatUtility.IsFloatDiffLessThan(loudness._lg, _lg, 0.02f) &&
               FloatUtility.IsFloatDiffLessThan(loudness._lo, _lo, 0.02f) &&
               FloatUtility.IsFloatDiffLessThan(loudness._gain, _gain, 0.02f) &&
               FloatUtility.IsFloatDiffLessThan(loudness._offset, _offset, 0.02f) &&
               Equals(_biquad, loudness._biquad);
    }

    public override int GetHashCode() => HashCode.Combine(_biquad);

    public object Clone()
    {
        var l = (Loudness)MemberwiseClone();
        l._biquad = (Biquad)_biquad.Clone();
        return l;
    }

    public void SetGain(float gain)
    {
        if (gain < 0.0f) gain = 0.0f;
        if (gain > 2.0f) gain = 2.0f;
        _gain = gain;
    }

    public float GetGain() => _gain;

    public void SetFreq(short freq) => _biquad.GetParams().SetFreq(freq);

    public short GetFreq() => _biquad.GetParams().GetFreq();

    public Biquad GetBiquad() => _biquad;

    public byte GetAddress() => TAS5558.LOUDNESS_LOG2_GAIN_REG;

    public string GetFreqInfo() => string.Format(CultureInfo.CurrentCulture, "{0}Hz", GetFreq());

    public string GetInfo() => string.Format(CultureInfo.CurrentCulture, "{0}%", (int)(_gain * 100));

    public void SendFreqToPeripheral(bool response) => _biquad.SendToPeripheral(response);

    public void SendToPeripheral(bool response)
    {
        var p = new BlePacket(GetMainDataBuf().GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    private HiFiToyDataBuf GetMainDataBuf()
    {
        var b = new byte[16];
        Array.Copy(Number523.Get523BigEnd(_lg), 0, b, 0, 4);
        Array.Copy(Number523.Get523BigEnd(_lo), 0, b, 4, 4);
        Array.Copy(Number523.Get523BigEnd(_gain), 0, b, 8, 4);
        Array.Copy(Number523.Get523BigEnd(_offset), 0, b, 12, 4);
        return new HiFiToyDataBuf(GetAddress(), b);
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var data = _biquad.GetDataBufs();
        data.Add(GetMainDataBuf());
        return data;
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        if (!_biquad.ImportFromDataBufs(dataBufs)) return false;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 16)
            {
                var b = buf.GetData();
                _lg = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 0, 4));
                _lo = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 4, 8));
                _gain = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 8, 12));
                _offset = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 12, 16));
                return true;
            }
        }

        return false;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("LG", _lg);
        xmlData.AddXmlElement("LO", _lo);
        xmlData.AddXmlElement("Gain", _gain);
        xmlData.AddXmlElement("Offset", _offset);
        xmlData.AddXmlData(_biquad.ToXmlData());

        var loudnessXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Address"] = ByteUtility.ToString(GetAddress()) };
        loudnessXmlData.AddXmlElement("Loudness", xmlData, attrib);
        return loudnessXmlData;
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

                if (elementName.Equals("Biquad", StringComparison.Ordinal))
                {
                    var addrStr = xmlParser.GetAttributeValue(null, "Address");
                    if (addrStr == null) continue;
                    var address = ByteUtility.Parse(addrStr);

                    if (_biquad.GetAddress() == address)
                    {
                        _biquad.ImportFromXml(xmlParser);
                        count++;
                    }
                }
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("Loudness", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("LG", StringComparison.Ordinal))
                {
                    _lg = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("LO", StringComparison.Ordinal))
                {
                    _lo = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("Gain", StringComparison.Ordinal))
                {
                    _gain = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("Offset", StringComparison.Ordinal))
                {
                    _offset = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 5)
        {
            throw new IOException("Loudness. Import from xml is not success.");
        }
    }
}
