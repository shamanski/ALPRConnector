using ConsoleApp1;
using Emgu.CV.Ocl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    public class CameraRepository
    {
        private readonly AppSettings _settings;

        public CameraRepository()
        {
            _settings = ConfigurationLoader.LoadSettings();
        }

        public void AddCamera(Camera camera)
        {

            var existingCameraIndex = _settings.Cameras.FindIndex(c => c.Name == camera.Name);

            if (existingCameraIndex != -1)
            {
                throw new ArgumentException($"Camera with name {camera.Name} already exists!");
            }
            else
            {
                _settings.Cameras.Add(camera);
                ConfigurationLoader.SaveSettings(_settings);
            }           
        }

        public void RemoveCamera(string name)
        {
            _settings.Cameras.Remove(_settings.Cameras.Where(c => c.Name == name).FirstOrDefault());
            ConfigurationLoader.SaveSettings(_settings);
        }

        public Camera GetCameraByName(string name)
        {
            return _settings.Cameras.FirstOrDefault(c => c.Name == name);
        }

        public List<Camera> GetAll()
        {
            return _settings.Cameras;
        }

        public string GetConnectionString(Camera camera)
        {
           var prefix = (String.IsNullOrEmpty(camera.Login) || String.IsNullOrEmpty(camera.Password)) ? String.Empty : String.Concat(camera.Login, ":", camera.Password, "@");
            return @"rtsp://192.168.1.184:554/user=admin&password=&channel=1&stream=1.sdp";
            //return String.Concat(@"rtsp://", prefix, camera.IpAddress, ":", camera.IpPort);
        }
    }
}
