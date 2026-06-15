using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Device;
using HPToy.Core.Numbers;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class Biquad : IHiFiToyObject, ICloneable
{
    private const int Fs = 96000;

    private bool _hiddenGui;
    private bool _enabled;
    private byte _address0;
    private byte _address1;
    private BiquadParam _params;

    public Biquad(byte address0, byte address1)
    {
        _hiddenGui = false;
        _enabled = true;
        _address0 = address0;
        _address1 = address1;
        _params = new BiquadParam();
    }

    public Biquad(byte address0) : this(address0, 0)
    {
    }

    public Biquad() : this(0, 0)
    {
    }

    public object Clone()
    {
        var b = (Biquad)MemberwiseClone();
        b._params = (BiquadParam)_params.Clone();
        return b;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Biquad biquad) return false;
        return _address0 == biquad._address0 &&
               _address1 == biquad._address1 &&
               Equals(_params, biquad._params);
    }

    public override int GetHashCode() => HashCode.Combine(_address0, _address1, _params);

    public void SetEnabled(bool enabled) => _enabled = enabled;

    public bool IsEnabled() => _enabled;

    public byte GetAddress() => _address0;

    public byte GetAddress1() => _address1;

    public BiquadParam GetParams() => _params;

    public void SendToPeripheral(bool response)
    {
        if (_params.GetTypeValue() == BiquadParam.Type.BIQUAD_USER)
        {
            var b = new byte[22];
            b[0] = _address0;
            b[1] = _address1;
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(2), _params.GetB0());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(6), _params.GetB1());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(10), _params.GetB2());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(14), _params.GetA1());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(18), _params.GetA2());

            BleClient.Instance.SendDataToDsp(b, true);
        }
        else
        {
            var b = new byte[14];
            b[0] = _address0;
            b[1] = _address1;

            if (_enabled)
            {
                b[2] = _params.GetOrderValue();
                b[3] = _params.GetTypeValue();
            }
            else
            {
                b[2] = BiquadParam.Order.BIQUAD_ORDER_2;
                b[3] = BiquadParam.Type.BIQUAD_OFF;
            }
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(4), _params.GetFreq());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(6), _params.GetQFac());
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(10), _params.GetDbVolume());

            BleClient.Instance.SendDataToDsp(b, response);
        }
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var data = _params.GetBinary();

        if (_address1 != 0)
        {
            return new List<HiFiToyDataBuf>
            {
                new(_address0, data),
                new(_address1, (byte[])data.Clone())
            };
        }

        return new List<HiFiToyDataBuf> { new(_address0, data) };
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == _address0 &&
                buf.GetLength() == 20 &&
                _params.ImportData(buf.GetData()))
            {
                return true;
            }
        }
        return false;
    }

    public float GetAFR(float freqX)
    {
        if (!_hiddenGui && _enabled)
        {
            return _params.GetAFR(freqX);
        }
        return 1.0f;
    }

    public string GetInfo() => _enabled ? _params.GetInfo() : "Biquad disabled.";

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("HiddenGui", _hiddenGui);
        xmlData.AddXmlElement("Order", _params.OrderValue);
        xmlData.AddXmlElement("Type", _params.TypeValue);
        xmlData.AddXmlElement("MaxFreq", _params.MaxFreq);
        xmlData.AddXmlElement("MinFreq", _params.MinFreq);
        xmlData.AddXmlElement("MaxQ", _params.MaxQ);
        xmlData.AddXmlElement("MinQ", _params.MinQ);
        xmlData.AddXmlElement("MaxDbVol", _params.MaxDbVolume);
        xmlData.AddXmlElement("MinDbVol", _params.MinDbVolume);
        xmlData.AddXmlElement("B0", _params.GetB0());
        xmlData.AddXmlElement("B1", _params.GetB1());
        xmlData.AddXmlElement("B2", _params.GetB2());
        xmlData.AddXmlElement("A1", _params.GetA1());
        xmlData.AddXmlElement("A2", _params.GetA2());

        var biquadXmlData = new XmlData();
        var attrib = new Dictionary<string, string>
        {
            ["Address"] = ByteUtility.ToString(_address0),
            ["Address1"] = ByteUtility.ToString(_address1)
        };
        biquadXmlData.AddXmlElement("Biquad", xmlData, attrib);
        return biquadXmlData;
    }

    public void ImportFromXml(XmlImportReader xmlParser)
    {
        string? elementName = null;
        var count = 0;
        float b0 = 1.0f, b1 = 0, b2 = 0, a1 = 0, a2 = 0;

        do
        {
            xmlParser.Next();

            if (xmlParser.EventType == XmlImportEventType.StartElement)
            {
                elementName = xmlParser.Name;
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("Biquad", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("HiddenGui", StringComparison.Ordinal))
                {
                    _hiddenGui = byte.Parse(elementValue, CultureInfo.InvariantCulture) == 1;
                    count++;
                }
                if (elementName.Equals("Order", StringComparison.Ordinal))
                {
                    _params.SetOrderValue(byte.Parse(elementValue, CultureInfo.InvariantCulture));
                    count++;
                }
                if (elementName.Equals("Type", StringComparison.Ordinal))
                {
                    _params.SetTypeValue(byte.Parse(elementValue, CultureInfo.InvariantCulture));
                    count++;
                }
                if (elementName.Equals("MaxFreq", StringComparison.Ordinal))
                {
                    _params.MaxFreq = short.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MinFreq", StringComparison.Ordinal))
                {
                    _params.MinFreq = short.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MaxQ", StringComparison.Ordinal))
                {
                    _params.MaxQ = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MinQ", StringComparison.Ordinal))
                {
                    _params.MinQ = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MaxDbVol", StringComparison.Ordinal))
                {
                    _params.MaxDbVolume = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MinDbVol", StringComparison.Ordinal))
                {
                    _params.MinDbVolume = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("B0", StringComparison.Ordinal))
                {
                    b0 = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("B1", StringComparison.Ordinal))
                {
                    b1 = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("B2", StringComparison.Ordinal))
                {
                    b2 = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("A1", StringComparison.Ordinal))
                {
                    a1 = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("A2", StringComparison.Ordinal))
                {
                    a2 = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 14)
        {
            throw new IOException("Biquad=" + _address0 + ". Import from xml is not success.");
        }
        _params.SetCoefs(b0, b1, b2, a1, a2);
    }

    public sealed class BiquadParam : ICloneable
    {
        private Order _order = new();
        private Type _type = new();
        private float _b0;
        private float _b1;
        private float _b2;
        private float _a1;
        private float _a2;
        private short _freq;
        private float _qFac;
        private float _dbVolume;
        internal short MaxFreq;
        internal short MinFreq;
        internal float MaxQ;
        internal float MinQ;
        internal float MaxDbVolume;
        internal float MinDbVolume;

        internal byte OrderValue => _order.GetValue();
        internal byte TypeValue => _type.GetValue();

        public BiquadParam()
        {
            SetBorderFreq(20000, 20);
            SetBorderQ(10.0f, 0.1f);
            SetBorderDbVolume(12.0f, -36.0f);
            _freq = 100;
            _qFac = 1.41f;
            _dbVolume = 0.0f;
            Update(_freq, _qFac, _dbVolume);
        }

        public BiquadParam(byte orderValue, byte typeValue, short freq, float qFac, float dbVolume) : this()
        {
            _order.SetValue(orderValue);
            _type.SetValue(typeValue);
            _freq = freq;
            _qFac = qFac;
            _dbVolume = dbVolume;
            CheckBorders();
            Update(_freq, _qFac, _dbVolume);
        }

        public BiquadParam(byte orderValue, byte typeValue, float b0, float b1, float b2, float a1, float a2) : this()
        {
            _order.SetValue(orderValue);
            _type.SetValue(typeValue);
            SetCoefs(b0, b1, b2, a1, a2);
        }

        public object Clone()
        {
            var p = (BiquadParam)MemberwiseClone();
            p._order = (Order)_order.Clone();
            p._type = (Type)_type.Clone();
            return p;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not BiquadParam that) return false;
            return FloatUtility.IsFloatDiffLessThan(that._b0, _b0, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(that._b1, _b1, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(that._b2, _b2, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(that._a1, _a1, 0.01f) &&
                   FloatUtility.IsFloatDiffLessThan(that._a2, _a2, 0.01f) &&
                   _freq == that._freq &&
                   FloatUtility.IsFloatDiffLessThan(that._qFac, _qFac, 0.02f) &&
                   FloatUtility.IsFloatDiffLessThan(that._dbVolume, _dbVolume, 0.02f) &&
                   MaxFreq == that.MaxFreq &&
                   MinFreq == that.MinFreq &&
                   FloatUtility.IsFloatDiffLessThan(that.MaxQ, MaxQ, 0.02f) &&
                   FloatUtility.IsFloatDiffLessThan(that.MinQ, MinQ, 0.02f) &&
                   FloatUtility.IsFloatDiffLessThan(that.MaxDbVolume, MaxDbVolume, 0.02f) &&
                   FloatUtility.IsFloatDiffLessThan(that.MinDbVolume, MinDbVolume, 0.02f) &&
                   Equals(_order, that._order) &&
                   Equals(_type, that._type);
        }

        public override int GetHashCode() => HashCode.Combine(_order, _type, _freq, MaxFreq, MinFreq);

        public void SetOrderValue(byte value)
        {
            _order.SetValue(value);
            Update(_freq, _qFac, _dbVolume);
        }

        public byte GetOrderValue() => _order.GetValue();

        public void SetTypeValue(byte value)
        {
            _type.SetValue(value);
            Update(_freq, _qFac, _dbVolume);
        }

        public byte GetTypeValue() => _type.GetValue();

        public void SetCoefs(float b0, float b1, float b2, float a1, float a2)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _a1 = a1;
            _a2 = a2;
            Update(b0, b1, b2, a1, a2);
        }

        public void SetB0(float b0)
        {
            if (GetTypeValue() == Type.BIQUAD_USER) _b0 = b0;
        }

        public float GetB0() => _b0;

        public void SetB1(float b1)
        {
            if (GetTypeValue() == Type.BIQUAD_USER) _b1 = b1;
        }

        public float GetB1() => _b1;

        public void SetB2(float b2)
        {
            if (GetTypeValue() == Type.BIQUAD_USER) _b2 = b2;
        }

        public float GetB2() => _b2;

        public void SetA1(float a1)
        {
            if (GetTypeValue() == Type.BIQUAD_USER) _a1 = a1;
        }

        public float GetA1() => _a1;

        public void SetA2(float a2)
        {
            if (GetTypeValue() == Type.BIQUAD_USER) _a2 = a2;
        }

        public float GetA2() => _a2;

        public void SetFreq(short freq)
        {
            if (freq < MinFreq) freq = MinFreq;
            if (freq > MaxFreq) freq = MaxFreq;
            _freq = freq;
            Update(_freq, _qFac, _dbVolume);
        }

        public short GetFreq() => _freq;

        public void SetFreqPercent(float percent)
        {
            SetFreq((short)Math.Pow(10, percent * (Math.Log10(MaxFreq) - Math.Log10(MinFreq)) + Math.Log10(MinFreq)));
        }

        public float GetFreqPercent() =>
            (float)((Math.Log10(_freq) - Math.Log10(MinFreq)) / (Math.Log10(MaxFreq) - Math.Log10(MinFreq)));

        public void SetQFac(float qFac)
        {
            if (qFac < MinQ) qFac = MinQ;
            if (qFac > MaxQ) qFac = MaxQ;
            _qFac = qFac;
            Update(_freq, _qFac, _dbVolume);
        }

        public float GetQFac() => _qFac;

        public void SetDbVolume(float dbVolume)
        {
            if (dbVolume < MinDbVolume) dbVolume = MinDbVolume;
            if (dbVolume > MaxDbVolume) dbVolume = MaxDbVolume;
            _dbVolume = dbVolume;
            Update(_freq, _qFac, _dbVolume);
        }

        public float GetDbVolume() => _dbVolume;

        public void SetBorderFreq(short maxFreq, short minFreq)
        {
            MaxFreq = maxFreq;
            MinFreq = minFreq;
        }

        public void SetBorderQ(float maxQ, float minQ)
        {
            MaxQ = maxQ;
            MinQ = minQ;
        }

        public void SetBorderDbVolume(float maxDbVolume, float minDbVolume)
        {
            MaxDbVolume = maxDbVolume;
            MinDbVolume = minDbVolume;
        }

        public void CheckBorders()
        {
            if (_freq < MinFreq) _freq = MinFreq;
            if (_freq > MaxFreq) _freq = MaxFreq;
            if (_qFac < MinQ) _qFac = MinQ;
            if (_qFac > MaxQ) _qFac = MaxQ;
            if (_dbVolume < MinDbVolume) _dbVolume = MinDbVolume;
            if (_dbVolume > MaxDbVolume) _dbVolume = MaxDbVolume;
        }

        private void Update(float b0, float b1, float b2, float a1, float a2)
        {
            float arg, w0;

            if (_order.GetValue() == Order.BIQUAD_ORDER_2)
            {
                switch (_type.GetValue())
                {
                    case Type.BIQUAD_LOWPASS:
                        arg = 2 * b1 / a1 + 1;
                        if (arg < 1.0f && arg > -1.0f) break;
                        w0 = (float)Math.Acos(1.0f / arg);
                        _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                        _qFac = (float)(Math.Sin(w0) * a1 / (2 * (2 * Math.Cos(w0) - a1)));
                        break;

                    case Type.BIQUAD_HIGHPASS:
                        arg = 2 * b1 / a1 + 1;
                        if (arg < 1.0f && arg > -1.0f) break;
                        w0 = (float)Math.Acos(-1.0f / arg);
                        _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                        _qFac = (float)(Math.Sin(w0) * a1 / (2 * (2 * Math.Cos(w0) - a1)));
                        break;

                    case Type.BIQUAD_PARAMETRIC:
                        arg = a1 / (b0 + b2);
                        if (arg > 1.0f || arg < -1.0f) break;
                        w0 = (float)Math.Acos(arg);
                        _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                        arg = (float)((b0 * 2 * Math.Cos(w0) - a1) / (2 * Math.Cos(w0) - a1));
                        if (arg < 0.0) break;
                        var ampl = Math.Sqrt(arg);
                        _dbVolume = (float)(40 * Math.Log10(ampl));
                        var alpha = (2 * Math.Cos(w0) / a1 - 1) * ampl;
                        _qFac = (float)(Math.Sin(w0) / (2 * alpha));
                        break;

                    case Type.BIQUAD_ALLPASS:
                        arg = a1 / (b0 + 1);
                        if (arg > 1.0f || arg < -1.0f) break;
                        w0 = (float)Math.Acos(arg);
                        _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                        _qFac = (float)(Math.Sin(w0) * a1 / (2 * (2 * Math.Cos(w0) - a1)));
                        break;

                    case Type.BIQUAD_BANDPASS:
                        w0 = (float)(Math.Acos(a1 / 2 * (1 + b0 / (1 - b0))));
                        _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                        break;
                }
            }
            else
            {
                if (a1 > 0)
                {
                    w0 = (float)(-Math.Log10(a1) / Math.Log10(2.7));
                    _freq = (short)Math.Round(w0 * Fs / (2 * Math.PI));
                }
            }
        }

        private void Update(short freq, float qFac, float dbVolume)
        {
            var w0 = 2 * (float)Math.PI * freq / Fs;
            float ampl;
            const float bandwidth = 1.41f;
            float alpha, a0;

            if (_type.GetValue() == Type.BIQUAD_USER) return;

            var s = (float)Math.Sin(w0);
            var c = (float)Math.Cos(w0);

            if (_order.GetValue() == Order.BIQUAD_ORDER_2)
            {
                switch (_type.GetValue())
                {
                    case Type.BIQUAD_LOWPASS:
                        alpha = s / (2 * qFac);
                        a0 = 1 + alpha;
                        _a1 = 2 * c / a0;
                        _a2 = (1 - alpha) / (-a0);
                        _b0 = (1 - c) / (2 * a0);
                        _b1 = (1 - c) / a0;
                        _b2 = (1 - c) / (2 * a0);
                        break;
                    case Type.BIQUAD_HIGHPASS:
                        alpha = s / (2 * qFac);
                        a0 = 1 + alpha;
                        _a1 = 2 * c / a0;
                        _a2 = (1 - alpha) / (-a0);
                        _b0 = (1 + c) / (2 * a0);
                        _b1 = (1 + c) / (-a0);
                        _b2 = (1 + c) / (2 * a0);
                        break;
                    case Type.BIQUAD_PARAMETRIC:
                        ampl = (float)Math.Pow(10, dbVolume / 40);
                        alpha = s / (2 * qFac);
                        a0 = 1 + alpha / ampl;
                        _a1 = 2 * c / a0;
                        _a2 = (1 - alpha / ampl) / (-a0);
                        _b0 = (1 + alpha * ampl) / a0;
                        _b1 = (2 * c) / (-a0);
                        _b2 = (1 - alpha * ampl) / a0;
                        break;
                    case Type.BIQUAD_ALLPASS:
                        alpha = s / (2 * qFac);
                        a0 = 1 + alpha;
                        _a1 = 2 * c / a0;
                        _a2 = (1 - alpha) / (-a0);
                        _b0 = (1 - alpha) / a0;
                        _b1 = 2 * c / (-a0);
                        _b2 = (1 + alpha) / a0;
                        break;
                    case Type.BIQUAD_BANDPASS:
                        alpha = (float)(s * Math.Sinh(0.3465735902 * bandwidth * w0 / s));
                        a0 = 1 + alpha;
                        _a1 = 2 * c / a0;
                        _a2 = (1 - alpha) / (-a0);
                        _b0 = alpha / a0;
                        _b1 = 0;
                        _b2 = -alpha / a0;
                        break;
                    case Type.BIQUAD_OFF:
                        _b0 = 1.0f;
                        _b1 = 0.0f;
                        _b2 = 0.0f;
                        _a1 = 0.0f;
                        _a2 = 0.0f;
                        break;
                }
            }
            else
            {
                _a2 = 0;
                _b2 = 0;

                switch (_type.GetValue())
                {
                    case Type.BIQUAD_LOWPASS:
                        _a1 = (float)Math.Pow(2.7, -w0);
                        _b0 = 1.0f - _a1;
                        break;
                    case Type.BIQUAD_HIGHPASS:
                        _a1 = (float)Math.Pow(2.7, -w0);
                        _b0 = _a1;
                        _b1 = -_a1;
                        break;
                    case Type.BIQUAD_ALLPASS:
                        _a1 = (float)Math.Pow(2.7, -w0);
                        _b0 = -_a1;
                        _b1 = 1.0f;
                        break;
                    case Type.BIQUAD_OFF:
                        _b0 = 1.0f;
                        _b1 = 0.0f;
                        _b2 = 0.0f;
                        _a1 = 0.0f;
                        _a2 = 0.0f;
                        break;
                }
            }
        }

        private void UpdateOrder()
        {
            FloatUtility.IsFloatNull(_b2);

            if (FloatUtility.IsFloatNull(_b2) && FloatUtility.IsFloatNull(_a2) &&
                !FloatUtility.IsFloatNull(_b0) && !FloatUtility.IsFloatNull(_b1) &&
                !FloatUtility.IsFloatNull(_a1))
            {
                _order.SetValue(Order.BIQUAD_ORDER_1);
            }
            else
            {
                _order.SetValue(Order.BIQUAD_ORDER_2);
            }
        }

        public byte[] GetBinary()
        {
            var b = new byte[20];
            Array.Copy(Number523.Get523BigEnd(_b0), 0, b, 0, 4);
            Array.Copy(Number523.Get523BigEnd(_b1), 0, b, 4, 4);
            Array.Copy(Number523.Get523BigEnd(_b2), 0, b, 8, 4);
            Array.Copy(Number523.Get523BigEnd(_a1), 0, b, 12, 4);
            Array.Copy(Number523.Get523BigEnd(_a2), 0, b, 16, 4);
            return b;
        }

        public bool ImportData(byte[] b)
        {
            if (b.Length == 20)
            {
                _b0 = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 0, 4));
                _b1 = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 4, 8));
                _b2 = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 8, 12));
                _a1 = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 12, 16));
                _a2 = Number523.ToFloat(BinaryOperation.CopyOfRange(b, 16, 20));
                UpdateOrder();
                Update(_b0, _b1, _b2, _a1, _a2);
                return true;
            }
            return false;
        }

        private float GetLPF(float freqX) =>
            (float)Math.Sqrt(1.0f / (Math.Pow(1 - Math.Pow(freqX / _freq, 2), 2) +
                                     Math.Pow(freqX / (_freq * _qFac), 2)));

        private float GetHPF(float freqX) =>
            (float)(Math.Sqrt(
                Math.Pow(Math.Pow(freqX / _freq, 4) - Math.Pow(freqX / _freq, 2), 2) +
                Math.Pow(freqX / _freq, 6) / Math.Pow(_qFac, 2)) /
                (Math.Pow(1 - Math.Pow(freqX / _freq, 2), 2) + Math.Pow(freqX / (_qFac * _freq), 2)));

        private float GetPEQ(float freqX)
        {
            var ampl = Math.Pow(10, _dbVolume / 40);
            var a1 = Math.Pow(1 - Math.Pow(freqX / _freq, 2), 2) + Math.Pow(freqX / (_qFac * _freq), 2);
            var a2 = (1 - Math.Pow(freqX / _freq, 2)) *
                     (freqX * ampl / (_qFac * _freq) - freqX / (ampl * _qFac * _freq));
            var b = Math.Pow(1 - Math.Pow(freqX / _freq, 2), 2) + Math.Pow(freqX / (ampl * _qFac * _freq), 2);
            return (float)(Math.Sqrt(Math.Pow(a1, 2) + Math.Pow(a2, 2)) / b);
        }

        private float GetCommonAFR(float freqX)
        {
            var w = 2.0 * Math.PI * freqX / Fs;
            var z1 = Complex.TrigonometricForm(1, w).Reciprocal();
            var z2 = z1.Mul(z1);

            var num = new Complex(_b0, 0).Add(new Complex(_b1, 0).Mul(z1)).Add(new Complex(_b2, 0).Mul(z2));
            var denom = new Complex(1, 0).Add(new Complex(-_a1, 0).Mul(z1)).Add(new Complex(-_a2, 0).Mul(z2));
            var h = num.Div(denom);
            return (float)h.Mod();
        }

        public float GetAFR(float freqX)
        {
            if (_order.GetValue() == Order.BIQUAD_ORDER_2)
            {
                return _type.GetValue() switch
                {
                    Type.BIQUAD_LOWPASS => GetLPF(freqX),
                    Type.BIQUAD_HIGHPASS => GetHPF(freqX),
                    Type.BIQUAD_PARAMETRIC => GetPEQ(freqX),
                    Type.BIQUAD_ALLPASS or Type.BIQUAD_OFF => 1.0f,
                    _ => GetCommonAFR(freqX)
                };
            }
            return GetCommonAFR(freqX);
        }

        public string GetInfo()
        {
            switch (_type.GetValue())
            {
                case Type.BIQUAD_LOWPASS:
                case Type.BIQUAD_HIGHPASS:
                case Type.BIQUAD_BANDPASS:
                case Type.BIQUAD_ALLPASS:
                    return _freq.ToString(CultureInfo.CurrentCulture) + "Hz";
                case Type.BIQUAD_PARAMETRIC:
                    return string.Format(CultureInfo.CurrentCulture, "{0}Hz Q:{1:F2} dB:{2:F1}",
                        _freq, _qFac, _dbVolume);
                case Type.BIQUAD_USER:
                    return string.Format(CultureInfo.CurrentCulture,
                        "b0:{0} b1:{1} b2:{2} a1:{3} a2:{4}", _b0, _b1, _b2, _a1, _a2);
                case Type.BIQUAD_OFF:
                    return "Biquad off.";
            }
            return "";
        }

        public sealed class Type : ICloneable
        {
            public const byte BIQUAD_LOWPASS = 2;
            public const byte BIQUAD_HIGHPASS = 1;
            public const byte BIQUAD_OFF = 0;
            public const byte BIQUAD_PARAMETRIC = 3;
            public const byte BIQUAD_ALLPASS = 4;
            public const byte BIQUAD_BANDPASS = 5;
            public const byte BIQUAD_USER = 6;
            public const byte BIQUAD_DEFAULT = BIQUAD_PARAMETRIC;

            private byte _value = BIQUAD_DEFAULT;

            public void SetValue(byte value)
            {
                if (value > BIQUAD_USER) value = BIQUAD_USER;
                if (value < BIQUAD_OFF) value = BIQUAD_OFF;
                _value = value;
            }

            public byte GetValue() => _value;

            public object Clone() => MemberwiseClone();

            public override bool Equals(object? obj) =>
                obj is Type type && _value == type._value;

            public override int GetHashCode() => _value.GetHashCode();
        }

        public sealed class Order : ICloneable
        {
            public const byte BIQUAD_ORDER_1 = 0;
            public const byte BIQUAD_ORDER_2 = 1;

            private byte _value = BIQUAD_ORDER_2;

            public void SetValue(byte value)
            {
                if (value > BIQUAD_ORDER_2) value = BIQUAD_ORDER_2;
                if (value < BIQUAD_ORDER_1) value = BIQUAD_ORDER_1;
                _value = value;
            }

            public byte GetValue() => _value;

            public object Clone() => MemberwiseClone();

            public override bool Equals(object? obj) =>
                obj is Order order && _value == order._value;

            public override int GetHashCode() => _value.GetHashCode();
        }
    }
}
