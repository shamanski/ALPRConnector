using AppDomain;
using Hsu.NullModemEmulator;

namespace AppDomain
{
    public class ModemEmulatorService : IDisposable
    {
        private readonly NullModemEmulatorManager _manager;
      public ModemEmulatorService()
        {
            _manager = new NullModemEmulatorManager();
        }

        public async Task AddPair(ComPortPair pair)
        {
            try
            {
                var ret = await _manager.AddPairAsync(
       new PortBuilder()
       .PortName(pair.Sender)
       .EmulateBaudRate(true)
        ,
       new PortBuilder()
       .PortName(pair.Receiver)
       .EmulateBaudRate(true)
          );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString() );
            }
            
        }

        public List<ComPortPair> GetPairs() 
        {
            return _manager.Pairs
                .Select(pair => new ComPortPair { Sender = pair.Value.A.PortName, Receiver = pair.Value.B.PortName, Name = ""})
                .ToList();
        }

        public void Dispose() 
        {

        }


    }
}
