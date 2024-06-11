using Emgu.CV.CvEnum;
using Emgu.CV;
using F23.StringSimilarity;
using openalprnet;
using System.Collections.Concurrent;
using AppDomain.Abstractions;
using Serilog;
using Serilog.Events;
using System.Threading.Channels;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Media.Media3D;

namespace AppDomain
{
    public class OpenAlprService : IAlprClient, IHealthCheckService
    {
        private readonly AlprNet _alprNet;
        private readonly ConcurrentQueue<string> _plates;
        private readonly LongestCommonSubsequence _comparer;
        private Mat _lastFrame;
        private readonly object _frameLock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private long frames = 0;
        private int threadId = Thread.CurrentThread.ManagedThreadId;
        private Stopwatch stopwatch = new Stopwatch();
        List<Rectangle> regions = new List<Rectangle>();

        public OpenAlprService()
        {
            _alprNet = new AlprNet("eu", string.Empty, string.Empty)
            {
                TopN = 10,
            };
            _plates = new ConcurrentQueue<string>();
            _comparer = new LongestCommonSubsequence();
            Rectangle rect = new Rectangle(0, 300, 720, 500);
            regions.Add(rect);
        }

        public bool IsLoaded => _alprNet.IsLoaded();

        public async Task StartProcessingAsync(VideoCapture videoCapture, Func<string, Task> processResult, CancellationToken cancellationToken)
        {

            Log.Information($"Starting processing camera");
            await processResult("CAMREADY");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var captureTask = Task.Run(async () =>
                {
                    await CaptureFramesAsync(videoCapture, cancellationToken);
                });

                var processingTask = Task.Run(async () =>
                {
                    await ProcessFramesAsync(cancellationToken);
                });

                var aggregationTask = Task.Run(async () =>
                {
                    await AggregatePlatesAsync(processResult, cancellationToken);
                });

                Log.Information("All tasks started");

                await Task.WhenAny(captureTask, processingTask, aggregationTask);
                Log.Information("One of the tasks has completed or canceled");
                throw new Exception();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in StartProcessingAsync: {ex.Message}");
                videoCapture.Dispose();
                throw;
            }
        }

        private async Task CaptureFramesAsync(VideoCapture videoCapture, CancellationToken cancellationToken)
        {
            DateTime lastFrameTime = DateTime.Now;
            Log.Information($"Capture on thread: {Thread.CurrentThread.ManagedThreadId}");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!videoCapture.IsOpened)
                {
                    Log.Error("Unable to open camera.");
                    throw new InvalidOperationException();
                }

                using var frame = new Mat();
                var isSuccess = videoCapture.Read(frame);

                if (frame == null)
                {
                    continue;
                }
                if (frame.Width == 0 || frame.Height == 0 || !isSuccess)
                {
                    var elapsedSeconds = (DateTime.Now - lastFrameTime).TotalSeconds;
                    if (elapsedSeconds > 5)
                    {
                        Log.Error($"Error reading stream");
                        throw new Exception("");
                    }

                    Thread.Sleep(20);
                    continue;
                }

                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = frame.Clone();
                    frame.Dispose();
                }
              
            }
            Log.Information($"Capture stopped");
        }

            private async Task ProcessFramesAsync(CancellationToken cancellationToken)
        {
            Log.Information($"Starting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_lastFrame != null)
                {
                    Mat frameToProcess;
                    lock (_frameLock)
                    {
                        frameToProcess = _lastFrame.Clone();
                    }

                    try
                    {
                        ProcessFrame(frameToProcess);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error while processing frame: {ex.Message}");
                    }
                    finally
                    {
                        frames++;
                        frameToProcess.Dispose();
                    }
                }

                Thread.Sleep(3);
            }

            Log.Information($"Exiting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
        }


        private void ProcessFrame(Mat frame)
        {
            byte[] imageBytes = null;

            try
            {
                lock (_frameLock)
                {
                    using var gray = new Mat();
                    CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
                    imageBytes = CvInvoke.Imencode(".jpg", gray);
                }

                byte[] prewarpedImage = _alprNet.PreWarp(imageBytes);
                var res = _alprNet.Recognize(prewarpedImage, regions);

                foreach (var plate in res.Plates)
                {
                    _plates.Enqueue(plate.BestPlate.Characters);     
                }
            }
            catch (Exception ex)
            {
                Log.Error("Frame error: " + ex.Message);
            }
            finally
            {
                frame?.Dispose();
            }
        }

        public void StopProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task AggregatePlatesAsync(Func<string, Task> processResult, CancellationToken cancellationToken)
        {
            Log.Information($"Starting AggregatePlatesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");

            var mostCommonPlate = string.Empty;
            var platesList = new List<string>(64);

            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(3000);                      
                while (_plates.TryDequeue(out var plate))
                {
                    platesList.Add(plate);
                }

                mostCommonPlate = AggregatePlates(platesList);
                if (mostCommonPlate != string.Empty)
                {
                    try
                    {                     
                        await processResult(mostCommonPlate);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error while processing result: {ex.Message}");
                    }
                }
                platesList.Clear();
            }

            Log.Information($"Exiting AggregatePlatesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
        }

        private string AggregatePlates(List<string> plates)
        {
            if (plates.Count < 5) return string.Empty;

            var plateGroups = new Dictionary<string, int>();
            foreach (var plate in plates)
            {
                bool found = false;
                foreach (var existingPlate in plateGroups.Keys.ToList())
                {
                    double similarity = _comparer.Distance(existingPlate, plate);
                    if (similarity < 4)
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
            var res = await Task.Run( () =>
            {
                VideoCapture videoCapture = null;
                    videoCapture = new VideoCapture(connection);
                    if (videoCapture.IsOpened)
                    {
                        return videoCapture;
                    }
                    else
                    {
                        Log.Error($"Error connecting to {connection}. Trying again");
                        videoCapture.Dispose();
                    }
                throw new ArgumentException("Unable to open video source");
            }, cancellationToken);
            return res;
        }
        public async Task<string> CheckHealthAsync()
        {
            await Task.Delay(10);
            int fps = 0;
            stopwatch.Stop();
            fps = frames == 0 ? 0 : (int)(frames / stopwatch.Elapsed.TotalSeconds);
            frames = 0;
            stopwatch.Restart();
            return $"Thread {threadId} LPR recognition service:  {fps} frames/sec";
        }

    }
}
