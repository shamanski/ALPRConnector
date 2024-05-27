using ConsoleApp1;
using Emgu.CV.Ocl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace AppDomain
{
    public class ComPortsManager
    {
        private readonly AppSettings _settings;

        public ComPortsManager()
        {
            _settings = ConfigurationLoader.LoadSettings();
        }

        public void AddPair(ComPortPair portPair)
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
            else
            {
                _settings.ComPortPairs.Add(portPair);
                ConfigurationLoader.SaveSettings(_settings);
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
