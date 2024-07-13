using AppDomain;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;

public class ComPortService : IDisposable, IHealthCheckService
{
    private readonly ConcurrentDictionary<string, SerialPort> _ports;
    private readonly ConcurrentDictionary<int, string> _rs485Addresses;
    private readonly ConcurrentDictionary<(string portName, int rs485Address), string> _lpDictionary;
    private readonly SemaphoreSlim _lpSemaphore = new SemaphoreSlim(1, 1);
    private long requests = 0;
    private int threadId = Thread.CurrentThread.ManagedThreadId;
    private byte[] answ;
    private byte[] readBuffer;
    Stopwatch stopwatch = new Stopwatch();

    public ComPortService()
    {

        _ports = new ConcurrentDictionary<string, SerialPort>();
        _rs485Addresses = new ConcurrentDictionary<int, string>();
        _lpDictionary = new ConcurrentDictionary<(string portName, int rs485Address), string>();
        Log.Information("COM port service started");
        answ = new byte[10];
        readBuffer = new byte[3];

    }

    public async Task Run(string portName)
    {
        if (!_ports.ContainsKey(portName))
        {
            var port = new SerialPort(portName, 19200, Parity.Even, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                RtsEnable = true,
                DtrEnable = true,
                ReadTimeout = 1000
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
            Log.Information($"{portName} opened");
        }

        var tcs = new TaskCompletionSource<bool>();
        var listenerThread =   new Thread(async () => await ListenPort(portName, tcs));
        listenerThread.Start();
        await tcs.Task;
    }

    private async Task ListenPort(string portName, TaskCompletionSource<bool> tcs)
    {
        try
        {
            Log.Information($"Starting COM port listening on thread: {Thread.CurrentThread.ManagedThreadId}");
            var serialPort = _ports[portName];
            byte caL;
            byte caC;
            byte caH;
            while (true)
            {
                Thread.Sleep(16);
                stopwatch.Start();
                if (serialPort.BytesToRead > 2)
                {
                    try
                    {
                        int i = await serialPort.BaseStream.ReadAsync(readBuffer, 0, 3);
                        if (i < 3) { continue; }
                        caH = readBuffer[0];
                        caL = readBuffer[1];
                        caC = readBuffer[2];
                    }
                    catch { continue; }

                    byte computedChecksum = (byte)(((caH ^ caL) ^ 0xFF) % 0x40);

                    if (caC == computedChecksum)
                    {
                        ProcessReceivedData(caH, caL, serialPort.PortName);
                    }
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

    public void RemoveRS485Address(int rs485Address)
    {
        if (_rs485Addresses.TryRemove(rs485Address, out var portName))
            Log.Information($"RS485 address {rs485Address} disconnected");
        {
            if (_ports.ContainsKey(portName))
            {
                var port = _ports[portName];
                if (_rs485Addresses.Values.Count(v => v == portName) == 0)
                {
                    port.Close();
                    _ports.TryRemove(portName, out _);
                    Log.Information($"{portName} closed");
                }
            }
        }
    }

    private void ProcessReceivedData(byte caH, byte caL, string comPortName)
    {
        requests++;
        int rs485Address = ExtractRs485Address(caH, caL);
        if (_ports.TryGetValue(comPortName, out var port))
        {
            if (_lpDictionary.TryRemove((comPortName, rs485Address), out var lp))
            {
                SendResponse(port, rs485Address, lp);
            }
            else
            {
                SendResponse(port, rs485Address, string.Empty);
            }
        }
    }

    private void SendResponse(SerialPort port, int rs485Address, string lp)
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

        port.Write(answ, 0, answ.Length);
        port.BaseStream.Flush();
        _lpDictionary.TryRemove((port.PortName, rs485Address), out _);

        if (!string.IsNullOrEmpty(lp))
        {
            Log.Debug($"{lp} sent to {port.PortName}, addr {rs485Address}");
        }
    }

    private int ExtractRs485Address(byte caH, byte caL)
    {
        int rs485Address = -1;

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
        await Task.Delay(10);
        int fps = 0;
        stopwatch.Stop();
        fps = requests == 0 ? 0 : (int)(requests / stopwatch.Elapsed.TotalSeconds);
        requests = 0;
        stopwatch.Restart();
        return $"Thread {threadId} COM Port service: listen {_ports.FirstOrDefault().Key}, get {fps} requests/sec";
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
