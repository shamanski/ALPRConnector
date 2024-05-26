using ConsoleApp1;
using Emgu.CV.Ocl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    public class CameraManager
    {
        private readonly AppSettings _settings;

        public CameraManager()
        {
            _settings = ConfigurationLoader.LoadSettings();
        }

        public Camera GetCameraByName(string name)
        {
            return _settings.Cameras.FirstOrDefault(c => c.Name == name);
        }
    }
}
