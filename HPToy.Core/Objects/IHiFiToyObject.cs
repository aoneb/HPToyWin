using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public interface IHiFiToyObject
{
    byte GetAddress();
    string GetInfo();
    void SendToPeripheral(bool response);
    List<HiFiToyDataBuf> GetDataBufs();
    bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs);
    XmlData ToXmlData();
    void ImportFromXml(XmlImportReader xmlParser);
}
