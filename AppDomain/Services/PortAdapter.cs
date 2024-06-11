using AppDomain;
using Emgu.CV;
using Serilog;
using System.Diagnostics;
using System.Threading;

public class PortAdapter : IDisposable, IHealthCheckService
{
    private readonly ComPortService _comPortService;
    private readonly LprReader _reader;
    private readonly CameraRepository cameraManager;
    private readonly LprReaderRepository readerManager;
    private readonly OpenAlprService alprClient;
    private CancellationTokenSource _cancellationTokenSource;

    public PortAdapter(ComPortService comPortService, LprReader reader)
    {
        _comPortService = comPortService;
        _reader = reader;
        cameraManager = new CameraRepository();
        readerManager = new LprReaderRepository();
        alprClient = new OpenAlprService();
        HealthCheck.RegisterService(alprClient);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task Run()
    {
        while (true)
        {
            try
            {
                var connection = cameraManager.GetConnectionString(_reader.Camera);
                Log.Information($"Trying connect to {connection}");
                using var videoCapture = await alprClient.CreateVideoCaptureAsync(connection, _cancellationTokenSource.Token);
                Log.Information($"Connected to {connection}");

                var portTask = Task.Run(async () =>
                {
                    await _comPortService.Run(_reader.ComPortPair.Sender);
                });

                var alprTask = Task.Run(async () =>
                {
                    await alprClient.StartProcessingAsync(videoCapture, async result =>
                    {
                        await _comPortService.SendLpAsync(_reader.ComPortPair.Sender, _reader.RS485Addr, result);
                    },
                _cancellationTokenSource.Token);
                });
                await Task.WhenAny(portTask, alprTask);
                throw new Exception("Corrupted");
            }
            catch (Exception ex)
            {
                Log.Error($"Connection unsuccessful: {ex.Message}");
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                continue;
            }
            Log.Information($"Reader started. Camera: {_reader.Name} COM: {_reader.ComPortPair.Sender} RS485: {_reader.RS485Addr}");
        }

    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        Log.Information($"Stop listen camera {_reader.Name}");
    }

    public async Task<string> CheckHealthAsync()
    {
        await Task.Delay(10);
        return $"Recognizing from reader: {_reader.Name} to {_reader.ComPortPair.Sender} at address {_reader.RS485Addr}";
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
        _comPortService.RemoveRS485Address(_reader.RS485Addr);
        SerialPortManager.CloseSerialPort(_reader.ComPortPair.Receiver);
    }
}
