using System.Collections.Concurrent;
using System.IO.Ports;

public class SerialPortManager
{
    private static readonly ConcurrentDictionary<string, SerialPort> _serialPorts = new ConcurrentDictionary<string, SerialPort>();
    private static readonly object _lock = new object();

    public static SerialPort GetSerialPort(string portName)
    {
        lock (_lock)
        {
            return _serialPorts.GetOrAdd(portName, (name) =>
            {
                SerialPort port = new SerialPort(name, 19200, Parity.Even, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    RtsEnable = true,
                    DtrEnable = true,
                    ReadTimeout = 100
                };
                port.Open();
                return port;
            });
        }
    }

    public static void CloseSerialPort(string portName)
    {
        lock (_lock)
        {
            if (_serialPorts.TryRemove(portName, out SerialPort port))
            {
                port.Close();
            }
        }
    }
}
