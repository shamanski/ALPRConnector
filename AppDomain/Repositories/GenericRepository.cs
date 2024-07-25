using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    public abstract class GenericRepository<T> where T : Model
    {
        private readonly List<T> _settings;

        public GenericRepository()
        {
            _settings = ConfigurationLoader.LoadSettings<T>();
        }

        public void Add(T item)
        {

            var existingIndex = _settings.FindIndex(c => c.Name == item.Name);

            if (existingIndex != -1)
            {
                throw new ArgumentException($"Item with name {item.Name} already exists!");
            }
            else
            {
                _settings.Add(item);
                ConfigurationLoader.SaveSettings();
            }
        }

        public void Update(T item)
        {
            var existing = _settings.FirstOrDefault(c => c.Name == item.Name);
            existing = item;
            ConfigurationLoader.SaveSettings();
        }

        public void Remove(string name)
        {
            _settings.Remove(_settings.Where(c => c.Name == name).FirstOrDefault());
            ConfigurationLoader.SaveSettings();
        }

        public T GetCByName(string name)
        {
            return _settings.FirstOrDefault(c => c.Name == name);
        }

        public List<T> GetAll()
        {
            return _settings;
        }
    }
}
