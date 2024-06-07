using Hsu.NullModemEmulator;

namespace ConsoleApp1
{
    public class ModemEmulator
    {
        private readonly NullModemEmulatorManager _manager;
    public ModemEmulator()
        {
            _manager = new NullModemEmulatorManager();
        }

        public async Task Initialize()
        {
            var ret = await _manager.AddPairAsync(
    new PortBuilder()
    .PortName("COM10")
    .EmulateBaudRate(true)
    ,
    new PortBuilder()
    .PortName("COM11")
    .EmulateBaudRate(true)
);
        }
    }
}
