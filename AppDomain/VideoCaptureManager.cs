using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Emgu.CV;
    using Emgu.CV.CvEnum;

    public class VideoCaptureManager : IDisposable
    {
        private readonly string _videoSource;
        private VideoCapture _videoCapture;
        private readonly object _lock = new object();

        public VideoCaptureManager(string videoSource)
        {
            _videoSource = videoSource;
            _videoCapture = new VideoCapture(_videoSource);
        }

        public async Task<Mat> CaptureFrameAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Mat frame = null;
                try
                {
                    lock (_lock)
                    {
                        if (_videoCapture == null || !_videoCapture.IsOpened)
                        {
                            _videoCapture?.Dispose();
                            _videoCapture = new VideoCapture(_videoSource);
                        }
                        frame = _videoCapture.QueryFrame();
                    }
                    if (frame != null && frame.Width > 0 && frame.Height > 0)
                    {
                        return frame;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error capturing frame: {ex.Message}");
                }
                await Task.Delay(1000, cancellationToken);
            }
            return null;
        }

        public void Dispose()
        {
            _videoCapture?.Dispose();
        }
    }
}
