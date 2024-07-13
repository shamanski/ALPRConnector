public interface IComPortService
{
    Task SendLpAsync(string ComPortName, int RS485Addr, string data);
}
