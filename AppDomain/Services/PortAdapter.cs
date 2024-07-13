using AppDomain;
using Serilog;
using System.Threading;

public class PortAdapter : IDisposable, IHealthCheckService
{
    private readonly ComPortService _comPortService;
    private readonly LprReader _reader;
    private readonly CameraRepository cameraManager;
    private readonly LprReaderRepository readerManager;
    private readonly string connection;
    private readonly OpenAlprService alprClient;
    private CancellationTokenSource _cancellationTokenSource;


    public PortAdapter(ComPortService comPortService, LprReader reader)
    {
        _comPortService = comPortService;
        _reader = reader;
        cameraManager = new CameraRepository();
        readerManager = new LprReaderRepository();
        connection = cameraManager.GetConnectionString(_reader.Camera);
        alprClient = new OpenAlprService(connection);
        HealthCheck.RegisterService(alprClient);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var portTask = Task.Run(async () =>
                {
                    await _comPortService.Run(_reader.ComPortPair.Sender);
                });

                var alprTask = Task.Run(async () =>
                {
                    await alprClient.StartProcessingAsync(async result =>
                    {
                        await _comPortService.SendLpAsync(_reader.ComPortPair.Sender, _reader.RS485Addr, result);
                    },
                _reader.Camera.Roi,
                cancellationToken);
                });
                await Task.WhenAny(portTask, alprTask);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                else 
                {
                    throw new Exception("Unknown error in LPR");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in LPR: {ex.Message}. Reconnect...");
            }
        }

    }

    public async Task<string> CheckHealthAsync()
    {
        await Task.Delay(10);
        return $"Recognizing from reader: {_reader.Name} to {_reader.ComPortPair.Sender} at address {_reader.RS485Addr}";
    }

    public void Dispose()
    {
        alprClient.Dispose();
    }
}
