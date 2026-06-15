using System.Xml;

namespace HPToy.Core.Xml;

public enum XmlImportEventType
{
    StartDocument,
    EndDocument,
    StartElement,
    EndElement,
    Text
}

public sealed class XmlImportReader : IDisposable
{
    private readonly XmlReader _reader;
    private XmlImportEventType _eventType = XmlImportEventType.StartDocument;
    private string _name = "";
    private string? _text;

    public XmlImportReader(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = false
        };
        _reader = XmlReader.Create(stream, settings);
    }

    public XmlImportReader(string xml)
        : this(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
    {
    }

    public XmlImportEventType EventType => _eventType;
    public string Name => _name;
    public string? Text => _text;

    public bool Next()
    {
        while (_reader.Read())
        {
            switch (_reader.NodeType)
            {
                case XmlNodeType.Element:
                    _name = _reader.LocalName;
                    _text = null;
                    _eventType = XmlImportEventType.StartElement;
                    return true;
                case XmlNodeType.EndElement:
                    _name = _reader.LocalName;
                    _text = null;
                    _eventType = XmlImportEventType.EndElement;
                    return true;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    _name = "";
                    _text = _reader.Value;
                    _eventType = XmlImportEventType.Text;
                    return true;
            }
        }

        _eventType = XmlImportEventType.EndDocument;
        return false;
    }

    public string? GetAttributeValue(string? ns, string name) => _reader.GetAttribute(name);

    public void Dispose() => _reader.Dispose();
}
