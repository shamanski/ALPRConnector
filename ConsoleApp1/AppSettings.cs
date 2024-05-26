
public class AppSettings
{
    public string ApiKey { get; set; } = new("");
    public int Timeout { get; set; }
    public ComPortSettings ComPortSettings { get; set; } = new();
    public List<CameraSettings> Cameras { get; set; } = new();
}

public class ComPortSettings
{
    public bool isVirtualPairUsing { get; set; }
    public string? SenderPortName { get; set; }
    public string? ReceiverPortName { get; set; }
}

public class CameraSettings
{
    public string Name { get; set; }
    public string Protocol { get; set; }
    public string IpAddress { get; set; }
    public string IpPort { get; set; }
    public string Login {  get; set; }
    public string Password { get; set; }
    public string RS485Address { get; set; }
}
