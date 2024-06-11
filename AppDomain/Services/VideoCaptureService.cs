using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDomain
{
    using System;
    using System.Collections.Concurrent;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using Emgu.CV;
    using Emgu.CV.CvEnum;
    using Serilog;
    using static System.Net.Mime.MediaTypeNames;

    public class VideoCaptureService
    {
        private static readonly Lazy<VideoCaptureService> _instance = new Lazy<VideoCaptureService>(() => new VideoCaptureService());
        public static VideoCaptureService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, VideoCapture> _captures = new ConcurrentDictionary<string, VideoCapture>();
        private readonly ConcurrentDictionary<string, List<Action<Mat>>> _frameHandlers = new ConcurrentDictionary<string, List<Action<Mat>>>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly object _lock = new object();

        private VideoCaptureService() { }

        public async Task StartProcessingAsync(string cameraAddress, Action<Mat> frameHandler)
        {
            if (!_captures.ContainsKey(cameraAddress))
            {
                await Task.Run(() =>
                {
                    var videoCapture = new VideoCapture(cameraAddress);
                    if (!videoCapture.IsOpened)
                    {
                        throw new ArgumentException("Unable to open video source");
                    }

                    lock (_lock)
                    {
                        _captures[cameraAddress] = videoCapture;
                        _frameHandlers[cameraAddress] = new List<Action<Mat>>();
                        _cancellationTokens[cameraAddress] = new CancellationTokenSource();
                    }

                    var captureTask = Task.Run(() => CaptureFrames(cameraAddress, _cancellationTokens[cameraAddress].Token));
                });
            }

            lock (_lock)
            {
                _frameHandlers[cameraAddress].Add(frameHandler);
            }
        }

        private async Task CaptureFrames(string cameraName, CancellationToken cancellationToken)
        {
            var videoCapture = _captures[cameraName];
            DateTime lastFrameTime = DateTime.Now;
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = new Mat();
                var isSuccess = videoCapture.Read(frame);

                if (frame.IsEmpty || !isSuccess)
                {
                    frame.Dispose();
                    await Task.Delay(10, cancellationToken);
                    var elapsedSeconds = (DateTime.Now - lastFrameTime).TotalSeconds;
                    if (elapsedSeconds > 10)
                    {
                        Log.Error($"Stream error in camera {cameraName}");
                        throw new InvalidOperationException();
                    }
                    continue;
                }

                lastFrameTime = DateTime.Now;
                List<Action<Mat>> handlers;
                lock (_lock)
                {
                    handlers = _frameHandlers[cameraName].ToList();
                }

                foreach (var handler in handlers)
                {
                    handler?.Invoke(frame.Clone());
                }

                frame.Dispose();
                await Task.Delay(10, cancellationToken);
            }
        }

        public void StopProcessing(string cameraName)
        {
            if (_cancellationTokens.TryRemove(cameraName, out var tokenSource))
            {
                tokenSource.Cancel();
                _captures.TryRemove(cameraName, out var videoCapture);
                videoCapture?.Dispose();
            }
        }

        public void StopAllProcessing()
        {
            foreach (var cameraName in _cancellationTokens.Keys.ToList())
            {
                StopProcessing(cameraName);
            }
        }

        public Mat ResizeFrame(Mat frame, Size newSize)
        {
            var resizedFrame = new Mat();
            CvInvoke.Resize(frame, resizedFrame, newSize);
            return resizedFrame;
        }
    }
}
