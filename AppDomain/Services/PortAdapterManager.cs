using Serilog;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Media.Animation;
namespace AppDomain;

public class PortAdapterManager
{
    private static readonly Lazy<PortAdapterManager> _instance =
        new Lazy<PortAdapterManager>(() => new PortAdapterManager());

    private readonly ConcurrentDictionary<LprReader, PortAdapter> _adapters;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources;
    private readonly ConcurrentDictionary<string, string> _adapterStatus;
    private readonly ComPortService _comPortService;
    private object _locker = new object();

    public static PortAdapterManager Instance => _instance.Value;

    public event EventHandler<AdapterStatusChangedEventArgs> AdapterStatusChanged;

    private PortAdapterManager()
    {
        _adapters = new ConcurrentDictionary<LprReader, PortAdapter>();
        _cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        _adapterStatus = new ConcurrentDictionary<string, string>();
        _comPortService = new ComPortService();
        //HealthCheck.RegisterService(_comPortService);
        Log.Information("LPR to COM adapter started");
    }

    public async Task StartAdapterAsync(LprReader reader)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        PortAdapter portAdapter;
        lock (_locker)
        {
            portAdapter = new PortAdapter(_comPortService, reader);
            //HealthCheck.RegisterService(portAdapter);
            if (_adapters.TryAdd(reader, portAdapter))
            {
                _cancellationTokenSources[reader.Name] = cancellationTokenSource;
                _adapterStatus[reader.Name] = "Running";
                AdapterStatusChanged?.Invoke(this, new AdapterStatusChangedEventArgs(reader, "Running"));               
            }            
        }
        await portAdapter.Run(cancellationTokenSource.Token);
    }

    public async Task  StopAdapterAsync(LprReader reader)
    {
        var adapterKeyValuePair = _adapters.FirstOrDefault(kv => kv.Key.Name == reader.Name);
        if (adapterKeyValuePair.Value != null)
        {
            var adapter = adapterKeyValuePair.Value;
            CancellationTokenSource cancelToken;
            _cancellationTokenSources.TryRemove(reader.Name, out cancelToken);
            cancelToken?.Cancel(); 
            await Task.Delay(1000);
            cancelToken?.Dispose();
            _adapterStatus[reader.Name] = "Stopped";
            AdapterStatusChanged?.Invoke(this, new AdapterStatusChangedEventArgs(reader, "Stopped"));
           _adapters.TryRemove(adapterKeyValuePair.Key, out _);
            adapter.Dispose();
            GC.Collect();
        }
    }


    public bool IsAdapterRunning(LprReader reader) => _adapters.ContainsKey(reader);

    public string GetAdapterStatus(LprReader reader)
    {
        _adapterStatus.TryGetValue(reader.Name, out var status);
        if (status == null) return "Stopped";
        return status;
    }
}

public class AdapterStatusChangedEventArgs : EventArgs
{
    public LprReader Reader { get; }
    public string Status { get; }

    public AdapterStatusChangedEventArgs(LprReader reader, string status)
    {
        Reader = reader;
        Status = status;
    }
}
