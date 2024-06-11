using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    public interface IHealthCheckService
    {
        Task<string> CheckHealthAsync();
    }
}
