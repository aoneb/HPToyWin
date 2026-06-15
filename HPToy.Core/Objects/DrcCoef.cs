using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class DrcCoef : IHiFiToyObject, ICloneable
{
    public const float POINT0_INPUT_DB = -120.0f;
    public const float POINT3_INPUT_DB = 0.0f;

    private byte _channel;
    private DrcPoint _point0;
    private DrcPoint _point1;
    private DrcPoint _point2;
    private DrcPoint _point3;

    public DrcCoef(byte channel, DrcPoint p0, DrcPoint p1, DrcPoint p2, DrcPoint p3)
    {
        SetChannel(channel);
        SetPoint0(p0);
        SetPoint1(p1);
        SetPoint2(p2);
        SetPoint3(p3);
    }

    public DrcCoef(byte channel)
        : this(channel,
            new DrcPoint(POINT0_INPUT_DB, -120.0f),
            new DrcPoint(-72.0f, -72.0f),
            new DrcPoint(-24.0f, -24.0f),
            new DrcPoint(POINT3_INPUT_DB, -24.0f))
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not DrcCoef drcCoef) return false;
        return _channel == drcCoef._channel &&
               Equals(_point0, drcCoef._point0) &&
               Equals(_point1, drcCoef._point1) &&
               Equals(_point2, drcCoef._point2) &&
               Equals(_point3, drcCoef._point3);
    }

    public override int GetHashCode() => _channel.GetHashCode();

    public object Clone()
    {
        var dc = (DrcCoef)MemberwiseClone();
        dc._point0 = (DrcPoint)_point0.Clone();
        dc._point1 = (DrcPoint)_point1.Clone();
        dc._point2 = (DrcPoint)_point2.Clone();
        dc._point3 = (DrcPoint)_point3.Clone();
        return dc;
    }

    public void SetChannel(byte channel)
    {
        if (channel > DrcChannel.DRC_CH_8) channel = DrcChannel.DRC_CH_8;
        if (channel < DrcChannel.DRC_CH_1_7) channel = DrcChannel.DRC_CH_1_7;
        _channel = channel;
    }

    public byte GetChannel() => _channel;

    public List<DrcPoint> GetPoints() => new() { _point0, _point1, _point2, _point3 };

    public void SetPoint0(DrcPoint p)
    {
        if (p.InputDb > 0) p.InputDb = 0;
        if (p.OutputDb > 0) p.OutputDb = 0;
        _point0 = p;
    }

    public DrcPoint GetPoint0() => _point0;

    public void SetPoint1(DrcPoint p)
    {
        if (p.InputDb > 0) p.InputDb = 0;
        if (p.OutputDb > 0) p.OutputDb = 0;
        _point1 = p;
    }

    public DrcPoint GetPoint1() => _point1;

    public void SetPoint2(DrcPoint p)
    {
        if (p.InputDb > 0) p.InputDb = 0;
        if (p.OutputDb > 0) p.OutputDb = 0;
        _point2 = p;
    }

    public DrcPoint GetPoint2() => _point2;

    public void SetPoint3(DrcPoint p)
    {
        if (p.InputDb > 0) p.InputDb = 0;
        if (p.OutputDb > 0) p.OutputDb = 0;
        _point3 = p;
    }

    public DrcPoint GetPoint3() => _point3;

    public void SetPoint0WithCheck(DrcPoint point0)
    {
        if (point0.OutputDb > _point1.OutputDb)
        {
            point0.OutputDb = _point1.OutputDb;
        }
        _point0.OutputDb = point0.OutputDb;
    }

    public void SetPoint1WithCheck(DrcPoint point1)
    {
        if (point1.InputDb > _point2.InputDb)
        {
            point1.InputDb = _point2.InputDb;
        }
        if (point1.OutputDb > _point2.OutputDb)
        {
            point1.OutputDb = _point2.OutputDb;
        }
        if (point1.OutputDb < _point0.OutputDb)
        {
            point1.OutputDb = _point0.OutputDb;
        }
        _point1 = point1;
    }

    public void SetPoint2WithCheck(DrcPoint point2)
    {
        if (point2.InputDb < _point1.InputDb)
        {
            point2.InputDb = _point1.InputDb;
        }
        if (point2.OutputDb > _point3.OutputDb)
        {
            point2.OutputDb = _point3.OutputDb;
        }
        if (point2.OutputDb < _point1.OutputDb)
        {
            point2.OutputDb = _point1.OutputDb;
        }
        _point2 = point2;
    }

    public void SetPoint3WithCheck(DrcPoint point3)
    {
        if (point3.OutputDb < _point2.OutputDb)
        {
            point3.OutputDb = _point2.OutputDb;
        }
        _point3.OutputDb = point3.OutputDb;
    }

    public byte GetAddress() =>
        _channel == DrcChannel.DRC_CH_8 ? TAS5558.DRC2_THRESHOLD_REG : TAS5558.DRC1_THRESHOLD_REG;

    public string GetInfo() =>
        "0:" + _point0.GetInfo() + " 1:" + _point1.GetInfo() +
        " 2:" + _point2.GetInfo() + " 3:" + _point3.GetInfo();

    public void SendToPeripheral(bool response)
    {
        var b = new byte[17];
        var offset = 0;
        b[offset++] = _channel;
        Array.Copy(_point0.Get88Binary(), 0, b, offset, 4);
        offset += 4;
        Array.Copy(_point1.Get88Binary(), 0, b, offset, 4);
        offset += 4;
        Array.Copy(_point2.Get88Binary(), 0, b, offset, 4);
        offset += 4;
        Array.Copy(_point3.Get88Binary(), 0, b, offset, 4);

        BleClient.Instance.SendDataToDsp(b, response);
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var b = new DrcParam(_point0, _point1, _point2, _point3).GetBinary();
        return new List<HiFiToyDataBuf> { new(GetAddress(), b) };
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 28)
            {
                var param = new DrcParam();
                if (param.ParseBinary(buf.GetData()))
                {
                    var p = param.GetDrcPoints();
                    _point0 = p[0];
                    _point1 = p[1];
                    _point2 = p[2];
                    _point3 = p[3];
                    return true;
                }
            }
        }

        return false;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("InputDb0", _point0.InputDb);
        xmlData.AddXmlElement("OutputDb0", _point0.OutputDb);
        xmlData.AddXmlElement("InputDb1", _point1.InputDb);
        xmlData.AddXmlElement("OutputDb1", _point1.OutputDb);
        xmlData.AddXmlElement("InputDb2", _point2.InputDb);
        xmlData.AddXmlElement("OutputDb2", _point2.OutputDb);
        xmlData.AddXmlElement("InputDb3", _point3.InputDb);
        xmlData.AddXmlElement("OutputDb3", _point3.OutputDb);

        var drcCoefXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Channel"] = _channel.ToString(CultureInfo.InvariantCulture) };
        drcCoefXmlData.AddXmlElement("DrcCoef", xmlData, attrib);
        return drcCoefXmlData;
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
                if (xmlParser.Name.Equals("DrcCoef", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("InputDb0", StringComparison.Ordinal))
                {
                    _point0.InputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("OutputDb0", StringComparison.Ordinal))
                {
                    _point0.OutputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("InputDb1", StringComparison.Ordinal))
                {
                    _point1.InputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("OutputDb1", StringComparison.Ordinal))
                {
                    _point1.OutputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("InputDb2", StringComparison.Ordinal))
                {
                    _point2.InputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("OutputDb2", StringComparison.Ordinal))
                {
                    _point2.OutputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("InputDb3", StringComparison.Ordinal))
                {
                    _point3.InputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("OutputDb3", StringComparison.Ordinal))
                {
                    _point3.OutputDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 8)
        {
            throw new IOException("DrcCoef=" + _channel + ". Import from xml is not success.");
        }
    }

    public sealed class DrcPoint : ICloneable
    {
        public float InputDb;
        public float OutputDb;

        public DrcPoint(float inputDb, float outputDb)
        {
            InputDb = inputDb;
            OutputDb = outputDb;
        }

        public object Clone() => MemberwiseClone();

        public override bool Equals(object? obj)
        {
            if (obj is not DrcPoint drcPoint) return false;
            return FloatUtility.IsFloatDiffLessThan(drcPoint.InputDb, InputDb, 0.5f) &&
                   FloatUtility.IsFloatDiffLessThan(drcPoint.OutputDb, OutputDb, 0.5f);
        }

        public override int GetHashCode() => 0;

        public void SetInputDb(float inputDb) => InputDb = inputDb;
        public float GetInputDb() => InputDb;
        public void SetOutputDb(float outputDb) => OutputDb = outputDb;
        public float GetOutputDb() => OutputDb;

        public string GetInfo() =>
            string.Format(CultureInfo.CurrentCulture, "{0:F1} {1:F1}", InputDb, OutputDb);

        public byte[] Get88Binary()
        {
            var b = new byte[4];
            Array.Copy(Number88.Get88LittleEnd(InputDb), 0, b, 0, 2);
            Array.Copy(Number88.Get88LittleEnd(OutputDb), 0, b, 2, 2);
            return b;
        }
    }

    public sealed class DrcParam : ICloneable
    {
        private float _threshold1Db;
        private float _threshold2Db;
        private float _offset1Db;
        private float _offset2Db;
        private float _k0;
        private float _k1;
        private float _k2;

        public DrcParam()
        {
            _threshold1Db = 0.0f;
            _threshold2Db = -1.0f;
            _offset1Db = 0.0f;
            _offset2Db = 0.0f;
            _k0 = 1.0f;
            _k1 = 1.0f;
            _k2 = 1.0f;
        }

        public DrcParam(DrcPoint p0, DrcPoint p1, DrcPoint p2, DrcPoint p3)
        {
            _threshold1Db = p1.InputDb;
            _threshold2Db = p2.InputDb;
            _offset1Db = p1.InputDb - p1.OutputDb;
            _offset2Db = p2.InputDb - p2.OutputDb;
            _k0 = GetK(p0, p1) - 1;
            _k1 = GetK(p1, p2) - 1;
            _k2 = GetK(p2, p3) - 1;
        }

        public DrcParam(byte[] data) : this()
        {
            ParseBinary(data);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DrcParam drcParam) return false;
            return FloatUtility.IsFloatDiffLessThan(drcParam._threshold1Db, _threshold1Db, 0.5f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._threshold2Db, _threshold2Db, 0.5f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._offset1Db, _offset1Db, 0.5f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._offset2Db, _offset2Db, 0.5f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._k0, _k0, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._k1, _k1, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(drcParam._k2, _k2, 0.01f);
        }

        public override int GetHashCode() => 0;

        public object Clone() => MemberwiseClone();

        private static float GetK(DrcPoint p0, DrcPoint p1)
        {
            if (p1.InputDb == p0.InputDb)
            {
                return 1;
            }
            return (p1.OutputDb - p0.OutputDb) / (p1.InputDb - p0.InputDb);
        }

        public DrcPoint[] GetDrcPoints()
        {
            var p = new DrcPoint[4];

            p[1] = new DrcPoint(_threshold1Db, _threshold1Db - _offset1Db);
            p[2] = new DrcPoint(_threshold2Db, _threshold2Db - _offset2Db);

            p[3] = new DrcPoint(POINT3_INPUT_DB, p[2].OutputDb + (_k2 + 1) * (POINT3_INPUT_DB - p[2].InputDb));
            p[0] = new DrcPoint(POINT0_INPUT_DB, p[1].OutputDb - (_k0 + 1) * (p[1].InputDb - POINT0_INPUT_DB));

            return p;
        }

        public byte[] GetBinary()
        {
            var b = new byte[28];
            var offset = 0;
            Array.Copy(Number923.Get923BigEnd(_threshold1Db / -6.0206f), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number923.Get923BigEnd(_threshold2Db / -6.0206f), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number523.Get523BigEnd(_k0), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number523.Get523BigEnd(_k1), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number523.Get523BigEnd(_k2), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number923.Get923BigEnd((_offset1Db + 24.0824f) / 6.0206f), 0, b, offset, 4);
            offset += 4;
            Array.Copy(Number923.Get923BigEnd((_offset2Db + 24.0824f) / 6.0206f), 0, b, offset, 4);
            return b;
        }

        public bool ParseBinary(byte[] data)
        {
            if (data.Length != 28) return false;

            _threshold1Db = Number923.ToFloat(BinaryOperation.CopyOfRange(data, 0, 4)) * -6.0206f;
            _threshold2Db = Number923.ToFloat(BinaryOperation.CopyOfRange(data, 4, 8)) * -6.0206f;
            _k0 = Number523.ToFloat(BinaryOperation.CopyOfRange(data, 8, 12));
            _k1 = Number523.ToFloat(BinaryOperation.CopyOfRange(data, 12, 16));
            _k2 = Number523.ToFloat(BinaryOperation.CopyOfRange(data, 16, 20));
            _offset1Db = Number923.ToFloat(BinaryOperation.CopyOfRange(data, 20, 24)) * 6.0206f - 24.0824f;
            _offset2Db = Number923.ToFloat(BinaryOperation.CopyOfRange(data, 24, 28)) * 6.0206f - 24.0824f;

            return true;
        }
    }
}
