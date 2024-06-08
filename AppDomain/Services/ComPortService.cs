using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ComPortService : IDisposable
{
    private readonly ConcurrentDictionary<string, (SerialPort port, SemaphoreSlim semaphore)> _ports;
    private readonly ConcurrentDictionary<int, string> _rs485Addresses;

    public ComPortService()
    {
        _ports = new ConcurrentDictionary<string, (SerialPort, SemaphoreSlim)>();
        _rs485Addresses = new ConcurrentDictionary<int, string>();
        Log.Information("COM port service started");
    }

    public async Task SendLpAsync(string portName, int rs485Address, string lp)
    {
        if (!_ports.ContainsKey(portName))
        {
            var serialPort = new SerialPort(portName, 19200, Parity.Even, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                RtsEnable = true,
                DtrEnable = true,
                ReadTimeout = 100
            };
            serialPort.Open();
            _ports[portName] = (serialPort, new SemaphoreSlim(1, 1));
            Log.Information($"COM port {portName} opened");
        }

        if (!_rs485Addresses.ContainsKey(rs485Address))
        {
            _rs485Addresses[rs485Address] = portName;
            Log.Information($"Connected to {portName} with RS485 device {rs485Address}");
        }

        var (port, semaphore) = _ports[portName];

        await semaphore.WaitAsync();
        try
        {
            byte caH = (byte)((rs485Address / 0x40) + 0xC0);
            byte caL = (byte)((rs485Address % 0x40) + 0x80);
            byte caC = (byte)(((caH ^ caL) ^ 0xFF) % 0x40);

            if (rs485Address < 0)
            {
                caH = 0x00;
                caL = (byte)((rs485Address % 0x40) + 0x80);
                caC = (byte)((caL ^ 0xFF) % 0x40);
            }

            List<byte> answ = new List<byte> { 0x40 };
            foreach (char ch in lp)
            {
                if (char.IsDigit(ch))
                {
                    answ.Add((byte)(ch - '0' + 1 + 0x40));
                }
                else if (char.IsLetter(ch))
                {
                    answ.Add((byte)(char.ToUpper(ch) - 'A' + 11 + 0x40));
                }
            }

            while (answ.Count < 9)
            {
                answ.Add(0x40);
            }

            byte chksum = 0xFF;
            foreach (byte x in answ)
            {
                chksum ^= x;
            }

            answ.Add((byte)(chksum % 0x40));
            port.BaseStream.Flush();
            await port.BaseStream.WriteAsync(answ.ToArray(), 0, answ.Count);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void RemoveRS485Address(int rs485Address)
    {
        if (_rs485Addresses.TryRemove(rs485Address, out var portName))
        {
            if (_ports.ContainsKey(portName))
            {
                var (port, semaphore) = _ports[portName];
                if (_rs485Addresses.Values.Count(v => v == portName) == 0)
                {
                    port.Close();
                    semaphore.Dispose();
                    _ports.TryRemove(portName, out _);
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var (port, semaphore) in _ports.Values)
        {
            port.Close();
            semaphore.Dispose();
        }
        _ports.Clear();
        _rs485Addresses.Clear();
    }
}
