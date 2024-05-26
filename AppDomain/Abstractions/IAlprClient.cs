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
        Task StartProcessingAsync(VideoCapture videoCapture, Action<string> processResult, CancellationToken cancellationToken);
        void StopProcessing();
    }
}
