namespace AppDomain;

public class AppSettings
{
    public string ApiKey { get; set; } = new("");
    public int Timeout { get; set; }
    public ComPortSettings ComPortSettings { get; set; } = new();
    public List<Camera> Cameras { get; set; } = new();
}

public class ComPortSettings
{
    public bool isVirtualPairUsing { get; set; }
    public string? SenderPortName { get; set; }
    public string? ReceiverPortName { get; set; }
}
