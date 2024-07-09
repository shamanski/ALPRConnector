
using Emgu.CV;
using F23.StringSimilarity;
using System.Collections.Concurrent;
using AppDomain.Abstractions;
using Serilog;
using System.Diagnostics;
using Rectangle = System.Drawing.Rectangle;
using Nomerator;
using Emgu.CV.CvEnum;
using System.Text.RegularExpressions;


namespace AppDomain
{
    public class OpenAlprService : IAlprClient, IHealthCheckService
    {
        private readonly DetectionAndReading _predictor;
        private readonly ConcurrentQueue<string> _plates;
        private readonly LongestCommonSubsequence _comparer;
        private readonly string _connection;
        private readonly object _frameLock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private long frames = 0;
        private int threadId = Thread.CurrentThread.ManagedThreadId;
        private Stopwatch stopwatch = new Stopwatch();
        List<Rectangle> regions = new List<Rectangle>();
        private object _framelock;

        public OpenAlprService(string connection)
        {
            _connection = connection;
            _predictor = new DetectionAndReading();
            _plates = new ConcurrentQueue<string>();
            _comparer = new LongestCommonSubsequence();
            Rectangle rect = new Rectangle(0, 100, 720, 500);
            regions.Add(rect);
        }

        public async Task StartProcessingAsync(Func<string, Task> processResult, CancellationToken cancellationToken)
        {
            
            Log.Information($"Starting processing camera {_connection}...");           

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {

                var processingTask = Task.Run(async () =>
                {
                    await ProcessFramesAsync(cancellationToken);
                });

                var aggregationTask = Task.Run(async () =>
                {
                    await AggregatePlatesAsync(processResult, cancellationToken);
                });

                Log.Information("All tasks started");

                await Task.WhenAny( processingTask, aggregationTask);
                Log.Information("One of the tasks has completed or canceled");
                throw new Exception();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in StartProcessingAsync: {ex.Message}");
                throw;
            }
        }

            private async Task ProcessFramesAsync(CancellationToken cancellationToken)
        {
            Log.Information($"Starting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
            DateTime lastFrameTime = DateTime.Now;
            using var videoCapture = new VideoCapture(_connection);
            videoCapture.Set(CapProp.Buffersize, 2);
            if (!videoCapture.IsOpened)
            {
                Log.Error("Error connecting to camera... Restart.");
                throw new Exception();
            }

            Log.Information($"Capture started on thread: {Thread.CurrentThread.ManagedThreadId}");
            using var frame = new Mat();
            var sw = new Stopwatch();
            //await processResult("CAMREADY");
            while (!cancellationToken.IsCancellationRequested)
            {
                videoCapture.Read(frame);
                if (frame == null)
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
                    using var frameToProcess = frame.Clone();
                    sw.Start();
                    var plates = _predictor.Recognize(frameToProcess);                   
                    foreach (var pl in plates)
                    {
                        _plates.Enqueue(pl);
                    }

                    sw.Stop();
                    Log.Debug($"Detection time:{sw.ElapsedMilliseconds} ms");
                    sw.Reset();
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
            int fps = frames == 0 ? 0 : (int)(frames / stopwatch.Elapsed.TotalSeconds);
            frames = 0;
            stopwatch.Restart();
            return $"Thread {threadId} LPR recognition service:  {fps} frames/sec";
        }     
    }
}
