using AppDomain;
using Serilog;

public class PortAdapter : IDisposable
{
    private readonly ComPortService _comPortService;
    private readonly LprReader _reader;
    private readonly CameraRepository cameraManager;
    private readonly LprReaderRepository readerManager;
    private readonly OpenAlprService alprClient;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public PortAdapter(ComPortService comPortService, LprReader reader)
    {
        _comPortService = comPortService;
        _reader = reader;
        cameraManager = new CameraRepository();
        readerManager = new LprReaderRepository();
        alprClient = new OpenAlprService();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task Run()
    {
        try
        {
            var connection = cameraManager.GetConnectionString(_reader.Camera);
            Log.Information($"Trying connect to {connection}");
            var videoCapture = await alprClient.CreateVideoCaptureAsync(connection, _cancellationTokenSource.Token);
            Log.Information($"Connected to {connection}");
            await alprClient.StartProcessingAsync(videoCapture, async result =>
            {
                await _comPortService.SendLpAsync(_reader.ComPortPair.Sender, _reader.RS485Addr, result);
            },
            _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Error($"Connection unsuccessful: {ex.Message}");
        }
        Log.Information($"Reader started. Camera: {_reader.Name} COM: {_reader.ComPortPair.Sender} RS485: {_reader.RS485Addr}");
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        Log.Information($"Stop listen camera {_reader.Name}");
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
        _comPortService.RemoveRS485Address(_reader.RS485Addr);
        SerialPortManager.CloseSerialPort(_reader.ComPortPair.Receiver);
    }
}
