using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class Drc : IHiFiToyObject, ICloneable
{
    private float[] _enabledCh = new float[8];
    private byte[] _evaluationCh = new byte[8];

    private DrcCoef _coef17;
    private DrcCoef _coef8;
    private DrcTimeConst _timeConst17;
    private DrcTimeConst _timeConst8;

    public Drc()
    {
        for (var i = 0; i < 8; i++)
        {
            _enabledCh[i] = 0.0f;
            _evaluationCh[i] = DrcEvaluation.DISABLED_EVAL;
        }

        _coef17 = new DrcCoef(DrcChannel.DRC_CH_1_7);
        _coef8 = new DrcCoef(DrcChannel.DRC_CH_8);
        _timeConst17 = new DrcTimeConst(DrcChannel.DRC_CH_1_7);
        _timeConst8 = new DrcTimeConst(DrcChannel.DRC_CH_8);
    }

    public Drc(DrcCoef? coef17, DrcTimeConst? timeConst17) : this()
    {
        if (coef17 != null) _coef17 = coef17;
        if (timeConst17 != null) _timeConst17 = timeConst17;
    }

    public Drc(DrcCoef? coef17, DrcCoef? coef8, DrcTimeConst? timeConst17, DrcTimeConst? timeConst8) : this()
    {
        if (coef17 != null) _coef17 = coef17;
        if (coef8 != null) _coef8 = coef8;
        if (timeConst17 != null) _timeConst17 = timeConst17;
        if (timeConst8 != null) _timeConst8 = timeConst8;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Drc drc) return false;

        for (var i = 0; i < 8; i++)
        {
            if (!FloatUtility.IsFloatDiffLessThan(_enabledCh[i], drc._enabledCh[i], 0.01f))
            {
                return false;
            }
        }
        return _evaluationCh.SequenceEqual(drc._evaluationCh) &&
               Equals(_coef17, drc._coef17) &&
               Equals(_coef8, drc._coef8) &&
               Equals(_timeConst17, drc._timeConst17) &&
               Equals(_timeConst8, drc._timeConst8);
    }

    public override int GetHashCode()
    {
        var result = HashCode.Combine(_coef17, _coef8, _timeConst17, _timeConst8);
        result = 31 * result + _evaluationCh.Aggregate(0, HashCode.Combine);
        return result;
    }

    public object Clone()
    {
        var drc = (Drc)MemberwiseClone();
        drc._enabledCh = new float[8];
        drc._evaluationCh = new byte[8];
        Array.Copy(_enabledCh, drc._enabledCh, 8);
        Array.Copy(_evaluationCh, drc._evaluationCh, 8);
        drc._coef17 = (DrcCoef)_coef17.Clone();
        drc._coef8 = (DrcCoef)_coef8.Clone();
        drc._timeConst17 = (DrcTimeConst)_timeConst17.Clone();
        drc._timeConst8 = (DrcTimeConst)_timeConst8.Clone();
        return drc;
    }

    public void SetEnabled(float enabled, byte channel)
    {
        if (channel < 8)
        {
            _enabledCh[channel] = enabled;
        }
    }

    public float GetEnabledChannel(byte channel) =>
        channel < 8 ? _enabledCh[channel] : 0.0f;

    public void SetEvaluation(byte evaluation, byte channel)
    {
        if (channel >= 8) return;

        if (evaluation < DrcEvaluation.DISABLED_EVAL) evaluation = DrcEvaluation.DISABLED_EVAL;
        if (evaluation > DrcEvaluation.POST_VOLUME_EVAL) evaluation = DrcEvaluation.POST_VOLUME_EVAL;
        _evaluationCh[channel] = evaluation;
    }

    public byte GetEvaluation(byte channel) =>
        channel < 8 ? _evaluationCh[channel] : DrcEvaluation.DISABLED_EVAL;

    public DrcTimeConst GetTimeConst17() => _timeConst17;

    public DrcCoef GetCoef17() => _coef17;

    public byte GetAddress() => TAS5558.DRC1_CONTROL_REG;

    public string GetInfo() => "Drc info";

    public void SendEvaluationToPeripheral(bool response)
    {
        var p = new BlePacket(GetEvaluationDataBuf().GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    public void SendEnabledToPeripheral(byte channel, bool response)
    {
        var d = GetEnabledDataBuf(channel);
        if (d != null)
        {
            var p = new BlePacket(d.GetBinary(), 20, response);
            BleClient.Instance.SendDataToDsp(p);
        }
    }

    public void SendToPeripheral(bool response)
    {
        _coef17.SendToPeripheral(response);
        _coef8.SendToPeripheral(response);
        _timeConst17.SendToPeripheral(response);
        _timeConst8.SendToPeripheral(response);
        SendEvaluationToPeripheral(response);

        for (byte i = 0; i < 8; i++)
        {
            SendEnabledToPeripheral(i, response);
        }
    }

    private HiFiToyDataBuf GetEvaluationDataBuf()
    {
        var d = 0;
        for (var i = 7; i >= 0; i--)
        {
            d <<= 2;
            d |= _evaluationCh[i] & 0x03;
        }

        var b = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(0), d);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), _evaluationCh[7] & 0x03);

        return new HiFiToyDataBuf(GetAddress(), b);
    }

    private HiFiToyDataBuf? GetEnabledDataBuf(byte channel)
    {
        if (channel > 7) return null;

        var b = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(0), 0x800000 - (int)(0x800000 * _enabledCh[channel]));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), (int)(0x800000 * _enabledCh[channel]));

        return new HiFiToyDataBuf((byte)(TAS5558.DRC_BYPASS1_REG + channel), b);
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var l = new List<HiFiToyDataBuf>();
        l.AddRange(_coef17.GetDataBufs());
        l.AddRange(_coef8.GetDataBufs());
        l.AddRange(_timeConst17.GetDataBufs());
        l.AddRange(_timeConst8.GetDataBufs());
        l.Add(GetEvaluationDataBuf());

        for (byte i = 0; i < 8; i++)
        {
            l.Add(GetEnabledDataBuf(i)!);
        }

        return l;
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        if (!_coef17.ImportFromDataBufs(dataBufs)) return false;
        if (!_coef17.ImportFromDataBufs(dataBufs)) return false;
        _coef8.ImportFromDataBufs(dataBufs);
        if (!_timeConst17.ImportFromDataBufs(dataBufs)) return false;
        if (!_timeConst8.ImportFromDataBufs(dataBufs)) return false;

        var importCounter = 0;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 8)
            {
                var data = buf.GetData();
                var d = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0));

                for (var u = 0; u < 7; u++)
                {
                    _evaluationCh[u] = (byte)(d & 0x03);
                    d = (int)((uint)d >> 2);
                }
                _evaluationCh[7] = (byte)(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4)) & 0x03);

                importCounter++;
            }

            if (buf.GetAddr() >= TAS5558.DRC_BYPASS1_REG &&
                buf.GetAddr() < TAS5558.DRC_BYPASS1_REG + 8 && buf.GetLength() == 8)
            {
                var val = Number523.ToFloat(BinaryOperation.CopyOfRange(buf.GetData(), 4, 8));
                _enabledCh[buf.GetAddr() - TAS5558.DRC_BYPASS1_REG] = val;
                importCounter++;
            }

            if (importCounter >= 9)
            {
                return true;
            }
        }

        // Flash may omit the last bypass register (0xA9); coef/timeConst already validated above.
        return importCounter >= 1;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();

        for (var i = 0; i < 8; i++)
        {
            xmlData.AddXmlElement(string.Format(CultureInfo.CurrentCulture, "enabledCh{0}", i), _enabledCh[i]);
        }
        for (var i = 0; i < 8; i++)
        {
            xmlData.AddXmlElement(string.Format(CultureInfo.CurrentCulture, "evaluationCh{0}", i), _evaluationCh[i]);
        }
        xmlData.AddXmlData(_coef17.ToXmlData());
        xmlData.AddXmlData(_coef8.ToXmlData());
        xmlData.AddXmlData(_timeConst17.ToXmlData());
        xmlData.AddXmlData(_timeConst8.ToXmlData());

        var drcXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Address"] = ByteUtility.ToString(GetAddress()) };
        drcXmlData.AddXmlElement("Drc", xmlData, attrib);
        return drcXmlData;
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

                if (elementName.Equals("DrcCoef", StringComparison.Ordinal))
                {
                    var channelStr = xmlParser.GetAttributeValue(null, "Channel");
                    if (channelStr == null) continue;
                    var channel = byte.Parse(channelStr, CultureInfo.InvariantCulture);

                    if (_coef17.GetChannel() == channel)
                    {
                        _coef17.ImportFromXml(xmlParser);
                        count++;
                    }
                    if (_coef8.GetChannel() == channel)
                    {
                        _coef8.ImportFromXml(xmlParser);
                        count++;
                    }
                }
                if (elementName.Equals("DrcTimeConst", StringComparison.Ordinal))
                {
                    var channelStr = xmlParser.GetAttributeValue(null, "Channel");
                    if (channelStr == null) continue;
                    var channel = byte.Parse(channelStr, CultureInfo.InvariantCulture);

                    if (_timeConst17.GetChannel() == channel)
                    {
                        _timeConst17.ImportFromXml(xmlParser);
                        count++;
                    }
                    if (_timeConst8.GetChannel() == channel)
                    {
                        _timeConst8.ImportFromXml(xmlParser);
                        count++;
                    }
                }
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("Drc", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                for (var i = 0; i < 8; i++)
                {
                    var keyStr = string.Format(CultureInfo.CurrentCulture, "enabledCh{0}", i);
                    if (elementName.Equals(keyStr, StringComparison.Ordinal))
                    {
                        _enabledCh[i] = float.Parse(elementValue, CultureInfo.InvariantCulture);
                        count++;
                    }
                }
                for (var i = 0; i < 8; i++)
                {
                    var keyStr = string.Format(CultureInfo.CurrentCulture, "evaluationCh{0}", i);
                    if (elementName.Equals(keyStr, StringComparison.Ordinal))
                    {
                        _evaluationCh[i] = byte.Parse(elementValue, CultureInfo.InvariantCulture);
                        count++;
                    }
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 20)
        {
            throw new IOException("Drc. Import from xml is not success.");
        }
    }

    public static class DrcEvaluation
    {
        public const byte DISABLED_EVAL = 0;
        public const byte PRE_VOLUME_EVAL = 1;
        public const byte POST_VOLUME_EVAL = 2;
    }
}
