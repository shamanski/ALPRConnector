namespace AppDomain;

public class AppSettings
{
    public string ApiKey { get; set; } = new("");
    public int Timeout { get; set; }
    public List<ComPortPair> ComPortPairs { get; set; } = new();
    public List<Camera> Cameras { get; set; } = new();
    public List<LprReader> LprReaders { get; set; } = new();
    public bool isVirtualPairUsing { get; set; }
    public Dictionary<string,string> ConnectionTemplates { get; set; } = new();
    public YoloConfig YoloConfig { get; set; } = new();
}

public class YoloConfig
{
    public int ImageHeight { get; set; } = 416;
    public int ImageWidth { get; set; } = 416;
    public int OutputTensorLength = 3549;
    public int IntraOpNumThreads { get; set; } = 2;
    public int InterOpNumThreads { get; set; } = 1;

}

public class CraftConfig
{

}

public class OcrConfig
{

}
