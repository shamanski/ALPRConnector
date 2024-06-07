using Emgu.CV.CvEnum;
using Emgu.CV;
using F23.StringSimilarity;
using openalprnet;
using System.Collections.Concurrent;
using AppDomain.Abstractions;
using Serilog;

namespace AppDomain
{
    public class OpenAlprService : IAlprClient
    {
        private readonly AlprNet _alprNet;
        private readonly ConcurrentQueue<string> _plates;
        private readonly LongestCommonSubsequence _comparer;
        private Mat _lastFrame;
        private readonly object _frameLock = new object();
        private CancellationTokenSource _cancellationTokenSource;

        public OpenAlprService()
        {
            _alprNet = new AlprNet("eu", string.Empty, string.Empty)
            {
                TopN = 10
            };

            _plates = new ConcurrentQueue<string>();
            _comparer = new LongestCommonSubsequence();
        }

        public bool IsLoaded => _alprNet.IsLoaded();

        public async Task StartProcessingAsync(VideoCapture videoCapture, Action<string> processResult, CancellationToken cancellationToken)
        {
            while (!videoCapture.IsOpened)
            {
                Console.WriteLine("Unable to open camera. Retrying...");
                videoCapture.Dispose();
                videoCapture = new VideoCapture(@"rtsp://admin:admin@192.168.107.166:8080");
                Thread.Sleep(1000);
            }

            Console.WriteLine("Camera connected...");

            _cancellationTokenSource = new CancellationTokenSource();

            var captureTask = Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var frame = new Mat();
                    videoCapture.Read(frame);

                    if (frame == null || frame.Width == 0 || frame.Height == 0)
                    {
                        frame.Dispose();
                        continue;
                    }

                    lock (_frameLock)
                    {
                        _lastFrame?.Dispose();
                        _lastFrame = frame;
                    }

                    Task.Delay(10).Wait();
                }
            }, cancellationToken);

            var processingTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Mat frameToProcess = null;

                    lock (_frameLock)
                    {
                        if (_lastFrame != null)
                        {
                            frameToProcess = _lastFrame.Clone();
                        }
                    }

                    if (frameToProcess != null)
                    {
                        try
                        {
                            ProcessFrame(frameToProcess);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing frame");
                        }
                        finally
                        {
                            frameToProcess.Dispose();
                        }
                    }

                    await Task.Delay(50, cancellationToken); // Adjust delay to control processing rate
                }
            }, cancellationToken);

            var aggregationTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(3000, cancellationToken); // Aggregation interval

                    var platesList = new List<string>();
                    while (_plates.TryDequeue(out var plate))
                    {
                        platesList.Add(plate);
                    }

                    var mostCommonPlate = AggregatePlates(platesList);
                    Console.WriteLine($"Best match: {mostCommonPlate}");
                    processResult(mostCommonPlate);
                }
            }, cancellationToken);

            await Task.WhenAll(captureTask, processingTask, aggregationTask);
        }

        private void ProcessFrame(Mat frame)
        {
            using var gray = new Mat();
            CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
            using var bmp = gray.ToBitmap();

            var res = _alprNet.Recognize(bmp);

            foreach (var plate in res.Plates)
            {
                _plates.Enqueue(plate.BestPlate.Characters);
            }
        }

        public void StopProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        private string AggregatePlates(List<string> plates)
        {
            if (plates.Count < 4) return "EMPTY";

            var plateGroups = new Dictionary<string, int>();
            foreach (var plate in plates)
            {
                bool found = false;
                foreach (var existingPlate in plateGroups.Keys.ToList())
                {
                    double similarity = _comparer.Distance(existingPlate, plate);
                    if (similarity < 3) // Customize threshold as needed
                    {
                        plateGroups[existingPlate]++;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    plateGroups[plate] = 1;
                }
            }

            var mostCommonPlate = plateGroups.OrderByDescending(p => p.Value).First().Key;
            Log.Information($"Detected: {mostCommonPlate}");
            return mostCommonPlate;
        }

        public async Task<VideoCapture> CreateVideoCaptureAsync(string connection, CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
            {
                VideoCapture videoCapture = null;
                while (!cancellationToken.IsCancellationRequested)
                {
                    videoCapture = new VideoCapture(connection);
                    if (videoCapture.IsOpened)
                    {
                        return videoCapture;
                    }
                    else
                    {
                        Log.Error($"Error connecting to {connection}. Trying again");
                        videoCapture.Dispose();
                        await Task.Delay(100, cancellationToken);
                    }
                }
                throw new ArgumentException("Unable to open video source");
            }, cancellationToken);
        }
    }
}
