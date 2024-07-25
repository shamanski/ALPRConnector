using Emgu.CV;
using Compunet.YoloV8;
using Rectangle = System.Drawing.Rectangle;
using System.Text;
using System.Data;
using System.Diagnostics;
using Serilog;

namespace Nomerator
{
    public class DetectionAndReading : IDisposable
    {
        private YoloV8Predictor localizationDetector;
        private CraftDetector keyPointsDetector;
        private DefaultCrnnTextRecognizer ocrDetector;
        private bool disposed = false;
        private Stopwatch stopwatch = new Stopwatch();

        public DetectionAndReading()
        {
            localizationDetector = YoloV8Predictor.Create("models/yolo8n.onnx");
            keyPointsDetector = new CraftDetector("models/craft.onnx");
            ocrDetector = new DefaultCrnnTextRecognizer("models/efficientnet_ocr.onnx");
        }

        public IEnumerable<string> Recognize(Mat frame)
        {
            /*Numberplate detection*/
            stopwatch.Start();
            var result = localizationDetector.Detect(frame);
              
            foreach (var entry in result.Boxes)
            {
                
                var r = entry.Bounds;
                Rectangle rect = new Rectangle(r.Left, r.Top, r.Width, r.Height);
                using Mat roiImage = new Mat(frame, rect);

                /*Numberplate box detection*/
                using var keypoints = keyPointsDetector.Detect(roiImage);
                stopwatch.Stop();
                var plate = new StringBuilder();
                foreach (var idx in keypoints.Boxes.Keys)
                {
                    var points = keypoints.Boxes[idx].Select(x => new System.Drawing.PointF(x.X * 1, x.Y * 1)).ToArray();
                    using var toOcr = keypoints.OutputImage.Clone();

                    /*Numberplate text recognition*/
                    var textBlock = ocrDetector.Recognize(toOcr, points);
                    
                    plate.Append(textBlock);
                }
                if (plate.Length == 0)
                {
                    continue;
                }

                if (plate.Length > 3 && plate.Length < 9)
                {
                    yield return plate.ToString();
                }
            }
            Log.Debug($"{stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Reset();
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
                    localizationDetector?.Dispose();
                    keyPointsDetector?.Dispose();
                    ocrDetector?.Dispose();
                }

                disposed = true;
            }
        }

        ~DetectionAndReading()
        {
            Dispose(false);
        }
    }
}