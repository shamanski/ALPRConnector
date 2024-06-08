using AppDomain;
using Serilog;
using System.Collections.Concurrent;
namespace AppDomain;

public class PortAdapterManager
{
    private static readonly Lazy<PortAdapterManager> _instance =
        new Lazy<PortAdapterManager>(() => new PortAdapterManager());

    private readonly ConcurrentDictionary<LprReader, PortAdapter> _adapters;
    private readonly ConcurrentDictionary<LprReader, CancellationTokenSource> _cancellationTokenSources;
    private readonly ConcurrentDictionary<string, string> _adapterStatus;
    private readonly ComPortService _comPortService;

    public static PortAdapterManager Instance => _instance.Value;

    public event EventHandler<AdapterStatusChangedEventArgs> AdapterStatusChanged;

    private PortAdapterManager()
    {
        _adapters = new ConcurrentDictionary<LprReader, PortAdapter>();
        _cancellationTokenSources = new ConcurrentDictionary<LprReader, CancellationTokenSource>();
        _adapterStatus = new ConcurrentDictionary<string, string>();
        _comPortService = new ComPortService();
        Log.Information("LPR to COM adapter started");
    }

    public async Task StartAdapterAsync(LprReader reader)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var portAdapter = new PortAdapter(_comPortService, reader);

        if (_adapters.TryAdd(reader, portAdapter))
        {
            _cancellationTokenSources[reader] = cancellationTokenSource;
            _adapterStatus[reader.Name] = "Starting...";
            AdapterStatusChanged?.Invoke(this, new AdapterStatusChangedEventArgs(reader, "Starting..."));
            await portAdapter.Run();
            _adapterStatus[reader.Name] = "Running";
            AdapterStatusChanged?.Invoke(this, new AdapterStatusChangedEventArgs(reader, "Running"));
        }
    }

    public void StopAdapter(LprReader reader)
    {
        var adapterKeyValuePair = _adapters.FirstOrDefault(kv => kv.Key.Name == reader.Name);
        if (adapterKeyValuePair.Value != null)
        {
            var adapter = adapterKeyValuePair.Value;
            adapter.Dispose();
            _adapterStatus[reader.Name] = "Stopped";
            AdapterStatusChanged?.Invoke(this, new AdapterStatusChangedEventArgs(reader, "Stopped"));

            _adapters.TryRemove(adapterKeyValuePair.Key, out _);
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
