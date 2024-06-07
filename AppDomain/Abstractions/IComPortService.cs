using System;
using System.Threading;
using System.Threading.Tasks;

public interface IComPortService
{
    Task SendLpAsync(string ComPortName, int RS485Addr, string data);
}
