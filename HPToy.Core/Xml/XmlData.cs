using System.Globalization;

namespace HPToy.Core.Xml;

public sealed class XmlData
{
    private string _xmlHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
    private readonly List<string> _xmlStringList = new();

    public void SetXmlHeader(string str) => _xmlHeader = str;
    public string GetXmlHeader() => _xmlHeader;

    public void Clear() => _xmlStringList.Clear();
    public int Size() => _xmlStringList.Count;

    public void AddString(string str) => _xmlStringList.Add(str);

    public string? Get(int index)
    {
        if (index >= Size()) return null;
        return _xmlStringList[index];
    }

    public override string ToString()
    {
        var strBuilder = new System.Text.StringBuilder();
        strBuilder.Append(_xmlHeader);
        for (var i = 0; i < Size(); i++)
        {
            strBuilder.Append(Get(i));
        }
        return strBuilder.ToString();
    }

    public void AddXmlData(XmlData? xmlData)
    {
        if (xmlData == null) return;
        for (var i = 0; i < xmlData.Size(); i++)
        {
            AddString(xmlData.Get(i)!);
        }
    }

    public void AddXmlElement(string name, XmlData? value, Dictionary<string, string>? attrib, int level)
    {
        var levelStr = GetLevelStr(level);
        var levelValueStr = GetLevelStr(level + 1);
        var attribStr = GetAttribStr(attrib);

        if (attribStr.Length > 0)
        {
            if (value != null)
            {
                AddString(levelStr + "<" + name + " " + attribStr + ">\n");
            }
            else
            {
                AddString(levelStr + "<" + name + " " + attribStr + "/>\n");
                return;
            }
        }
        else
        {
            if (value != null)
            {
                AddString(levelStr + "<" + name + ">\n");
            }
            else
            {
                AddString(levelStr + "<" + name + "/>\n");
                return;
            }
        }

        for (var i = 0; i < value.Size(); i++)
        {
            AddString(levelValueStr + value.Get(i));
        }

        AddString(levelStr + "</" + name + ">\n");
    }

    public void AddXmlElement(string name, string value, Dictionary<string, string>? attrib, int level)
    {
        var levelStr = GetLevelStr(level);
        var attribStr = GetAttribStr(attrib);

        var elementStr = attribStr.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, levelStr + "<" + name + " " + attribStr + ">" + value + "</" + name + ">\n")
            : string.Format(CultureInfo.InvariantCulture, levelStr + "<" + name + ">" + value + "</" + name + ">\n");

        AddString(elementStr);
    }

    public void AddXmlElement(string name, int value, Dictionary<string, string>? attrib, int level) =>
        AddXmlElement(name, value.ToString(CultureInfo.InvariantCulture), attrib, level);

    public void AddXmlElement(string name, double value, Dictionary<string, string>? attrib, int level) =>
        AddXmlElement(name, value.ToString(CultureInfo.InvariantCulture), attrib, level);

    public void AddXmlElement(string name, bool value, Dictionary<string, string>? attrib, int level) =>
        AddXmlElement(name, value ? 1 : 0, attrib, level);

    public void AddXmlElement(string name, XmlData? value, Dictionary<string, string>? attrib) =>
        AddXmlElement(name, value, attrib, 0);

    public void AddXmlElement(string name, string value, Dictionary<string, string>? attrib) =>
        AddXmlElement(name, value, attrib, 0);

    public void AddXmlElement(string name, int value, Dictionary<string, string>? attrib) =>
        AddXmlElement(name, value, attrib, 0);

    public void AddXmlElement(string name, double value, Dictionary<string, string>? attrib) =>
        AddXmlElement(name, value, attrib, 0);

    public void AddXmlElement(string name, bool value, Dictionary<string, string>? attrib) =>
        AddXmlElement(name, value, attrib, 0);

    public void AddXmlElement(string name, XmlData? value) => AddXmlElement(name, value, null, 0);
    public void AddXmlElement(string name, string value) => AddXmlElement(name, value, null, 0);
    public void AddXmlElement(string name, int value) => AddXmlElement(name, value, null, 0);
    public void AddXmlElement(string name, double value) => AddXmlElement(name, value, null, 0);
    public void AddXmlElement(string name, bool value) => AddXmlElement(name, value, null, 0);
    public void AddXmlElement(string name, float value) => AddXmlElement(name, value.ToString(CultureInfo.InvariantCulture), null, 0);

    private static string GetLevelStr(int level)
    {
        return new string('\t', level);
    }

    private static string GetAttribStr(Dictionary<string, string>? attrib)
    {
        if (attrib == null) return "";
        var attribStr = new System.Text.StringBuilder();
        foreach (var entry in attrib)
        {
            attribStr.Append(entry.Key).Append("=\"").Append(entry.Value).Append("\" ");
        }
        return attribStr.ToString();
    }
}
