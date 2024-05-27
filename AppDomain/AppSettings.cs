namespace AppDomain;

public class AppSettings
{
    public string ApiKey { get; set; } = new("");
    public int Timeout { get; set; }
    public List<ComPortPair> ComPortPairs { get; set; } = new();
    public List<Camera> Cameras { get; set; } = new();
    public List<LprReader> LprReaders { get; set; } = new();
    public bool isVirtualPairUsing { get; set; }
}
