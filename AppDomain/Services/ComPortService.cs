using AppDomain;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;


public class ComPortService : IDisposable, IHealthCheckService
{
    private readonly ConcurrentDictionary<string, SerialPort> _ports;
    private readonly ConcurrentDictionary<string, List<int> > _rs485Addresses;
    private readonly ConcurrentDictionary<(string portName, int rs485Address), string> _lpDictionary;
    private readonly SemaphoreSlim _lpSemaphore = new SemaphoreSlim(1, 1);
    private readonly object portLock = new object();
    private long requests = 0;
    private int threadId = Thread.CurrentThread.ManagedThreadId;
    private byte[] answ;
    private byte[] readBuffer;
    private Thread listenerThread;
    private TaskCompletionSource<bool> tcs;
    private BlockingCollection<byte[]> outputQueue = new BlockingCollection<byte[]>();
    SerialPort _port;
    Stopwatch sw;

    public ComPortService()
    {
        _ports = new ConcurrentDictionary<string, SerialPort>();
        _rs485Addresses = new ConcurrentDictionary<string, List<int>>();
        _lpDictionary = new ConcurrentDictionary<(string portName, int rs485Address), string>();
        Log.Information("COM port service started");
        answ = new byte[10];
        readBuffer = new byte[3];
        sw = new Stopwatch();
    }

    public async Task Run(string portName, int rs485Address)
    {
        if (!_ports.ContainsKey(portName))
        {
            lock (portLock)
            {
                var port = new SerialPort(portName, 19200, Parity.Even, 8, StopBits.One) 
                {
                    Handshake = Handshake.None,
                    RtsEnable = true,
                    DtrEnable = true,
                    ReadTimeout = 1000,
                    ReadBufferSize= 4,
                    WriteBufferSize= 10,
                    ReceivedBytesThreshold = 1,                   
                };
                try
                {
                    port.Open();
                }
                catch (Exception ex)
                {
                    Log.Error($"{portName} unavailable: {ex.Message}");
                    throw;
                }

                _ports[portName] = port;
                _port = port;
                Log.Information($"{portName} opened");

                tcs = new TaskCompletionSource<bool>();
                listenerThread = new Thread(async () => await ListenPort(portName, tcs))
                {
                    Priority = ThreadPriority.Highest,
                    IsBackground = true,                   
                };
                
                listenerThread.Start();
                threadId = listenerThread.ManagedThreadId;
            }
        }

        if (_rs485Addresses.TryGetValue(portName, out var addresses))
        {
            addresses.Add(rs485Address);
        }
        else
        {
            _rs485Addresses.TryAdd(portName, [rs485Address]);
        }
        await tcs.Task;
    }


    

    private async Task ListenPort(string portName, TaskCompletionSource<bool> tcs)
    {
        try
        {
            await Task.Delay(5000);
            Log.Information($"Starting COM port listening on thread: {Thread.CurrentThread.ManagedThreadId}");
            var serialPort = _ports[portName];
            serialPort.BaseStream.Flush();
            byte caL;
            byte caC;
            byte caH;
            while (true)
            {
                    try
                    {
                    await serialPort.BaseStream.ReadAsync(readBuffer, 0, 3).ConfigureAwait(false);
                    caH = readBuffer[0];
                    caL = readBuffer[1];
                    caC = readBuffer[2];
                     
                }
                    catch { continue; }

                    byte computedChecksum = (byte)(((caH ^ caL) ^ 0xFF) % 0x40);
                    while (computedChecksum != caC) 
                    {
                        caH = caL;
                        caL = caC;
                        await serialPort.BaseStream.ReadAsync(readBuffer, 0 , 1).ConfigureAwait(false);
                        caC = readBuffer[0];
                        computedChecksum = (byte)(((caH ^ caL) ^ 0xFF) % 0x40);
                    }

                sw.Reset();
                sw.Start();
                int rs485Address = ExtractRs485Address(caH, caL);              

                if (caC == computedChecksum && _rs485Addresses[portName].Contains(rs485Address))
                    {                        
                        await ProcessReceivedData(serialPort.PortName, rs485Address).ConfigureAwait(false);
                    }
                }
     
        }

        finally
        {
            tcs.TrySetResult(true);
        }
    }

    public async Task SendLpAsync(string portName, int rs485Address, string lp)
    {
        await _lpSemaphore.WaitAsync();
        try
        {
            _lpDictionary[(portName, rs485Address)] = lp;
        }
        finally
        {
            _lpSemaphore.Release();
        }
    }

   /* public void RemoveRS485Address(string port, int rs485Address)
    {
        if (_rs485Addresses.TryRemove(port, out var Rs))
            Log.Information($"RS485 address {rs485Address} disconnected");
        {
            if (_ports.ContainsKey(port))
            {
                var portToClose = _ports[port];
                if (_rs485Addresses.Values.Count(v => v == port) == 0)
                {
                    portToClose.Close();
                    _ports.TryRemove(portToClose, out _);
                    Log.Information($"{portName} closed");
                }
            }
        }
    }*/

    private async Task ProcessReceivedData(string comPortName, int rs485Address)
    {
        
          requests++;
           if (_ports.TryGetValue(comPortName, out var port))
           {
               if (_lpDictionary.TryRemove((comPortName, rs485Address), out var lp))
               {
                   await SendResponse(port, rs485Address, lp).ConfigureAwait(false);
               }
               else
               {
                  await SendResponse(port, rs485Address, string.Empty).ConfigureAwait(false);
               }
           }
    }

    private async Task SendResponse(SerialPort port, int rs485Address, string lp)
    {
        
        answ[0] = 0x40;
        int index = 1;
        foreach (char ch in lp)
        {
            if (char.IsDigit(ch))
            {
                answ[index++] = (byte)(ch - '0' + 1 + 0x40);
            }
            else if (char.IsLetter(ch))
            {
                answ[index++] = (byte)(char.ToUpper(ch) - 'A' + 11 + 0x40);
            }
        }

        while (index < 9)
        {
            answ[index++] = 0x40;
        }

        byte chksum = 0xFF;
        for (int i = 0; i < 9; i++)
        {
            chksum ^= answ[i];
        }

        answ[9] = (byte)(chksum % 0x40);
        sw.Stop();
        if (sw.ElapsedMilliseconds < 15)
        {
            //await Task.Delay(20);
            await Task.Delay(3);
            await port.BaseStream.WriteAsync(answ, 0, answ.Length);
            await port.BaseStream.FlushAsync();
            if (!string.IsNullOrEmpty(lp))
            {
                Log.Debug($"{lp} sent to {port.PortName}, addr {rs485Address}");
                await Task.Delay(40);
            }
            _lpDictionary.TryRemove((port.PortName, rs485Address), out _);
            
        }        
        
        else
        {
            Log.Debug($"Slow response: {sw.ElapsedMilliseconds} ms");
        }

        
    }

    private int ExtractRs485Address(byte caH, byte caL)
    {
        int rs485Address;

        if ((caH & 0x80) == 0x80)
        {
            rs485Address = ((caH & 0x3F) << 6) | (caL & 0x3F);
        }
        else
        {
            rs485Address = caH & 0x7F;
        }

        return rs485Address;
    }

    public async Task<string> CheckHealthAsync()
    {
        await Task.Delay(1000);
        return $"Thread {threadId} COM Port service: listen {_ports.FirstOrDefault().Key}";
    }

    public void Dispose()
    {
        foreach (var port in _ports.Values)
        {
            port.Close();
        }
        _ports.Clear();
        _rs485Addresses.Clear();
    }
}
