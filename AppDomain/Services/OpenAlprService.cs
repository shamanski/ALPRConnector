using Emgu.CV;
using F23.StringSimilarity;
using System.Collections.Concurrent;
using AppDomain.Abstractions;
using Serilog;
using System.Diagnostics;
using Rectangle = System.Drawing.Rectangle;
using Nomerator;
using Emgu.CV.CvEnum;


namespace AppDomain
{
    public class OpenAlprService : IAlprClient, IHealthCheckService, IDisposable
    {
        private readonly DetectionAndReading _predictor;
        private readonly ConcurrentQueue<string> _plates;
        private readonly LongestCommonSubsequence _comparer;
        private readonly string _connection;
        private long frames = 0;
        private int threadId = Thread.CurrentThread.ManagedThreadId;
        private Stopwatch stopwatch = new Stopwatch();
        private List<string> platesList = new List<string>(64);
        private bool disposed;

        public OpenAlprService(string connection)
        {
            _connection = connection;
            _predictor = new DetectionAndReading();
            _plates = new ConcurrentQueue<string>();
            _comparer = new LongestCommonSubsequence();
        }

        public async Task StartProcessingAsync(Func<string, Task> processResult, RelativeRectangle roi, CancellationToken cancellationToken)
        {           
            Log.Information($"Starting processing camera {_connection}...");           
            try
            {

                var processingTask = Task.Run(async () =>
                {
                    await ProcessFramesAsync(processResult, roi, cancellationToken);
                });

                var aggregationTask = Task.Run(async () =>
                {
                    await AggregatePlatesAsync(processResult, cancellationToken);
                });

                Log.Information("All tasks started");

                await Task.WhenAny( processingTask, aggregationTask);
                Log.Information("One of the tasks has completed or canceled");
                if (cancellationToken.IsCancellationRequested ) 
                {
                    return;
                }

                throw new Exception();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in StartProcessingAsync: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessFramesAsync(Func<string, Task> processResult, RelativeRectangle roi, CancellationToken cancellationToken)
        {
            Log.Information($"Starting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
            DateTime lastFrameTime = DateTime.Now;
            using var videoCapture = new VideoCapture(_connection);
            videoCapture.Set(CapProp.Buffersize, 2.0);
            if (!videoCapture.IsOpened)
            {
                Log.Error("Error connecting to camera... Restart.");
                throw new Exception();
            }

            Log.Information($"Capture started on thread: {Thread.CurrentThread.ManagedThreadId}");
            using var frame = new Mat();
            var sw = new Stopwatch();
            await processResult("CAMREADY");
            var roiRect = new Rectangle()
            {
                X = (int)(roi.RelativeRoiLeft * videoCapture.Width),
                Y = (int)(roi.RelativeRoiTop * videoCapture.Height),
                Width = (int)(roi.RelativeRoiWidth * videoCapture.Width),
                Height = (int)(roi.RelativeRoiHeight * videoCapture.Height)
            };
            while (!cancellationToken.IsCancellationRequested)
            {
                videoCapture.Set(CapProp.PosFrames, videoCapture.Get(CapProp.FrameCount) - 1);
                videoCapture.Read(frame);
                if (frame == null || frame.IsEmpty)
                {
                    var elapsedSeconds = (DateTime.Now - lastFrameTime).TotalSeconds;
                    if (elapsedSeconds > 5)
                    {
                        Log.Error($"Error reading stream");
                        throw new Exception("Failed to read from camera.");
                    }

                    continue;
                }
                try
                {
                    using Mat roiImage = new Mat(frame, roiRect);
                    foreach (var plate in _predictor.Recognize(roiImage))
                    {
                        _plates.Enqueue(plate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error while processing frame: {ex.Message}");
                }
                finally
                {

                    frames++;
                }

                await Task.Delay(20);
            }

            Log.Information($"Exiting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
        }

        private async Task AggregatePlatesAsync(Func<string, Task> processResult, CancellationToken cancellationToken)
        {
            Log.Information($"Starting AggregatePlatesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");

            var mostCommonPlate = string.Empty;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(3000);                      
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
            if (plates == null || plates.Count == 0) return string.Empty;
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

        public async Task<string> CheckHealthAsync()
        {
            await Task.Delay(10);
            stopwatch.Stop();
            double fps = frames == 0 ? 0.0 : frames / stopwatch.Elapsed.TotalSeconds;
            frames = 0;
            stopwatch.Restart();
            return $"Thread {threadId} LPR recognition service:  {fps} frames/sec";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _predictor.Dispose();
                }

                disposed = true;
            }
        }

        ~OpenAlprService()
        {
            Dispose(false);
        }
    }
}
