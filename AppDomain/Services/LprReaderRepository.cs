using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    public class LprReaderRepository
    {
        private readonly AppSettings _settings;

        public LprReaderRepository()
        {
            _settings = ConfigurationLoader.LoadSettings();
        }

        public void AddReader(LprReader reader)
        {
            if (reader.ComPortPair.Sender == reader.ComPortPair.Receiver)
            {
                throw new ArgumentException("Choose different ports for the ComPortPair!");
            }

            var existingReaderIndex = _settings.LprReaders.FindIndex(r => r.Name == reader.Name);

            if (existingReaderIndex != -1)
            {
                throw new ArgumentException($"LPR Reader with name {reader.Name} already exists!");
            }
            else
            {
                _settings.LprReaders.Add(reader);
                ConfigurationLoader.SaveSettings(_settings);
            }
        }

        public void RemoveReader(string name)
        {
            var readerToRemove = _settings.LprReaders.FirstOrDefault(r => r.Name == name);
            if (readerToRemove != null)
            {
                _settings.LprReaders.Remove(readerToRemove);
                ConfigurationLoader.SaveSettings(_settings);
            }
        }

        public LprReader GetReaderByName(string name)
        {
            return _settings.LprReaders.FirstOrDefault(r => r.Name == name);
        }

        public List<LprReader> GetAll()
        {
            return _settings.LprReaders;
        }
    }
}

