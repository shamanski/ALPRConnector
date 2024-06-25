using Emgu.CV.CvEnum;
using Emgu.CV;
using F23.StringSimilarity;
using System.Collections.Concurrent;
using AppDomain.Abstractions;
using Serilog;
using System.Diagnostics;
using Rectangle = System.Drawing.Rectangle;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Nomerator;
using Image = SixLabors.ImageSharp.Image;

namespace AppDomain
{
    public class OpenAlprService : IAlprClient, IHealthCheckService
    {
        private readonly DetectionAndReading _predictor;
        private readonly Tesseract _ocr;
        private readonly ConcurrentQueue<string> _plates;
        private readonly LongestCommonSubsequence _comparer;
        private readonly string _connection;
        private Mat _lastFrame;
        private readonly object _frameLock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private long frames = 0;
        private int threadId = Thread.CurrentThread.ManagedThreadId;
        private Stopwatch stopwatch = new Stopwatch();
        List<System.Drawing.Rectangle> regions = new List<System.Drawing.Rectangle>();
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

            Log.Information($"Starting processing camera...");           

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var captureTask = Task.Run(async () =>
                {
                    await CaptureFramesAsync(processResult, cancellationToken);
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
                throw;
            }
        }

        private async Task CaptureFramesAsync(Func<string, Task> processResult, CancellationToken cancellationToken)
        {
            DateTime lastFrameTime = DateTime.Now;            
            using var videoCapture = new VideoCapture(_connection);
            if (!videoCapture.IsOpened)
            {
                Log.Error("Error connecting to camera... Restart.");
                throw new Exception();
            }

            Log.Information($"Capture started on thread: {Thread.CurrentThread.ManagedThreadId}");
            await processResult("CAMREADY");
            Mat frame = new Mat();
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(100);

                if (!videoCapture.IsOpened)
                {
                    Log.Error("Unable to open camera.");
                    throw new InvalidOperationException();
                }

                frame = videoCapture.QueryFrame();
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

                lock (_frameLock)
                {
                    _lastFrame = frame.Clone();
                }

                frame.Dispose();
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
                       // _lastFrame.Dispose();
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

                Thread.Sleep(30);
            }

            Log.Information($"Exiting ProcessFramesAsync on thread: {Thread.CurrentThread.ManagedThreadId}");
        }


        private void ProcessFrame(Mat frame)
        {
            {
                List<string> plates = new();                
                plates = _predictor.Recognize(frame);
                foreach (var pl in plates)
                {
                    _plates.Enqueue(pl);     
                }
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
                Thread.Sleep(2000);                      
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
            int fps = 0;
            stopwatch.Stop();
            fps = frames == 0 ? 0 : (int)(frames / stopwatch.Elapsed.TotalSeconds);
            frames = 0;
            stopwatch.Restart();
            return $"Thread {threadId} LPR recognition service:  {fps} frames/sec";
        }

        private static UMat FilterPlate(UMat plate)
        {
            UMat thresh = new UMat();
            CvInvoke.Threshold(plate, thresh, 120, 255, ThresholdType.BinaryInv);
            //Image<Gray, Byte> thresh = plate.ThresholdBinaryInv(new Gray(120), new Gray(255));

            System.Drawing.Size plateSize = plate.Size;
            using (Mat plateMask = new Mat(plateSize.Height, plateSize.Width, DepthType.Cv8U, 1))
            using (Mat plateCanny = new Mat())
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                plateMask.SetTo(new MCvScalar(255.0));
                CvInvoke.Canny(plate, plateCanny, 100, 50);
                CvInvoke.FindContours(plateCanny, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                int count = contours.Size;
                for (int i = 1; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {

                        Rectangle rect = CvInvoke.BoundingRectangle(contour);
                        if (rect.Height > (plateSize.Height >> 1))
                        {
                            rect.X -= 1; rect.Y -= 1; rect.Width += 2; rect.Height += 2;
                            Rectangle roi = new Rectangle(System.Drawing.Point.Empty, plate.Size);
                            rect.Intersect(roi);
                            CvInvoke.Rectangle(plateMask, rect, new MCvScalar(), -1);
                            //plateMask.Draw(rect, new Gray(0.0), -1);
                        }
                    }

                }

                thresh.SetTo(new MCvScalar(), plateMask);
            }

            CvInvoke.Erode(thresh, thresh, null, new System.Drawing.Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
            CvInvoke.Dilate(thresh, thresh, null, new System.Drawing.Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

            return thresh;
        }

    }
}
