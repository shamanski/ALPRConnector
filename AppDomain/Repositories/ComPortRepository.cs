using System.IO.Ports;

namespace AppDomain
{
    public class ComPortRepository : GenericRepository<ComPortPair>
    {
        private readonly AppSettings _settings;
        private ModemEmulatorService _emulatorService;

        public ComPortRepository(ModemEmulatorService emulatorService)
        {
            _settings = ConfigurationLoader.LoadSettings();
            this._emulatorService = emulatorService;
        }

        public async Task AddPair(ComPortPair portPair)
        {
            if (portPair.Sender == portPair.Receiver)
            {
                throw new ArgumentException("Choose different ports!");
            }

            portPair.Name =String.IsNullOrEmpty( portPair.Name ) ? portPair.Sender + "-" + portPair.Receiver : portPair.Name;
            var existingPairIndex = _settings.ComPortPairs.FindIndex(c => c.Name == portPair.Name);

            if (existingPairIndex != -1)
            {
                throw new ArgumentException($"Port pair already exists!");
            }

             if (await _emulatorService.AddPair(portPair))
            {
                _settings.ComPortPairs.Add(portPair);
                ConfigurationLoader.SaveSettings(_settings);
            }
            else
            {
                throw new Exception("Can't add ports to Windows. Check if application has admin privileges");
            }
         
        }

        public void RemovePair(string name)
        {
            _settings.ComPortPairs.Remove(_settings.ComPortPairs.Where(c => c.Name == name).FirstOrDefault());
            ConfigurationLoader.SaveSettings(_settings);
        }

        public ComPortPair GetPairByName(string name)
        {
            return _settings.ComPortPairs.FirstOrDefault(c => c.Name == name);
        }

        public List<ComPortPair> GetAll()
        {
            return _settings.ComPortPairs;
        }

        public List<string> GetFreePorts()
        {
            var list = new List<string>(30);
            var availablePorts = SerialPort.GetPortNames();
            for (var i = 1; i < 31 ; i++)
            {
                var port = "COM" + i.ToString();
                if (!availablePorts.Contains(port))
                list.Add(port);
            }

            return list;
        }
    }
}
