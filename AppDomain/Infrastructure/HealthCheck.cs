using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AppDomain
{
    public static class HealthCheck
    {
        private static readonly List<IHealthCheckService> _healthCheckServices = new List<IHealthCheckService>();

        public static void RegisterService(IHealthCheckService service)
        {
            _healthCheckServices.Add(service);
        }

        public static async Task<Dictionary<string, string>> CheckAllServicesAsync()
        {
            var results = new Dictionary<string, string>();
            foreach (var service in _healthCheckServices)
            {
                var result = await service.CheckHealthAsync();
                results[service.GetType().Name + "_" + service.GetHashCode()] = result;
            }
            return results;
        }
    }
}
