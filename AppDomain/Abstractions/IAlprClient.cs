using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain.Abstractions
{
    public interface IAlprClient
    {
        Task StartProcessingAsync(VideoCapture videoCapture, Func<string, Task> processResult, CancellationToken cancellationToken);
        void StopProcessing();
    }
}
