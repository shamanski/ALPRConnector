using Microsoft.VisualBasic;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ComPortService : IComPortService
{
    private readonly SerialPort _serialPort;
    private readonly int _cardAddr;
    byte caH, caL, caC;

    public ComPortService(string portName, int RS485Addr)
    {
        _serialPort = new SerialPort(portName, 19200, Parity.Even, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            RtsEnable = true,
            DtrEnable = true,
            ReadTimeout = 100
        };
        _cardAddr = RS485Addr;
        _serialPort.Open();
       // _serialPort.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);

        caH = (byte)((_cardAddr / 0x40) + 0xC0);
        caL = (byte)((_cardAddr % 0x40) + 0x80);
        caC = (byte)(((caH ^ caL) ^ 0xFF) % 0x40);

        if (_cardAddr < 0)
        {
            caH = 0x00;
            caL = (byte)((_cardAddr % 0x40) + 0x80);
            caC = (byte)((caL ^ 0xFF) % 0x40);
        }
    }

    public async Task SendLpAsync(string lp)
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
  
        Task.Delay(10);
        byte[] odpyt_addr_h = new byte[1];
        byte[] odpyt_addr_l = new byte[1];
        byte[] odpyt_chksum = new byte[1];
        bool odpyt_addr_recognized = false;

        if (caH != 0x00)
        {
            _serialPort.Read(odpyt_addr_h, 0, 1);
            if (odpyt_addr_h[0] == caH)
            {
                _serialPort.Read(odpyt_addr_l, 0, 1);
                if (odpyt_addr_l[0] == caL)
                {
                    _serialPort.Read(odpyt_chksum, 0, 1);
                    if (odpyt_chksum[0] == caC)
                    {
                        odpyt_addr_recognized = true;
                    }
                }
            }
        }
        else
        {
            _serialPort.Read(odpyt_addr_l, 0, 1);
            if (odpyt_addr_l[0] == caL)
            {
                _serialPort.Read(odpyt_chksum, 0, 1);
                if (odpyt_chksum[0] == caC)
                {
                    odpyt_addr_recognized = true;
                }
            }
        }

        if (odpyt_addr_recognized)
        {
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
            _serialPort.BaseStream.Flush();
            await _serialPort.BaseStream.WriteAsync(answ.ToArray(), 0, answ.Count);
            Console.WriteLine($"Sent: {BitConverter.ToString(answ.ToArray())}");
        }
    }


    private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        int bytesToRead = sp.BytesToRead;
        byte[] buffer = new byte[bytesToRead];
        sp.Read(buffer, 0, bytesToRead);
        string receivedData = Encoding.ASCII.GetString(buffer);
        Console.WriteLine($"Received: {receivedData}");
    }

}

