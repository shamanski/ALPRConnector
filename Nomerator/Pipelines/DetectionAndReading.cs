using Emgu.CV;
using Compunet.YoloV8;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;
using Emgu.CV.CvEnum;
using System.Text;
using Size = System.Drawing.Size;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Data;
using static System.Net.Mime.MediaTypeNames;
using Emgu.CV.Shape;

namespace Nomerator
{
    public class DetectionAndReading : IDisposable
    {
        private YoloV8Predictor localizationDetector;
        private CraftDetector keyPointsDetector;
        private DefaultCrnnTextRecognizer ocrDetector;
        private bool disposed = false;

        public DetectionAndReading()
        {
            localizationDetector = YoloV8Predictor.Create("model.onnx");
            keyPointsDetector = new CraftDetector("attempt-craft1.onnx");
            ocrDetector = new DefaultCrnnTextRecognizer("CRNN_VGG_BiLSTM_CTC.onnx");
        }

        public IEnumerable<string> Recognize(Mat frame)
        {
            /*Numberplate detection*/
            var result = localizationDetector.Detect(frame, 1.0);
               
            foreach (var entry in result.Boxes)
            {
                var r = entry.Bounds;
                Rectangle rect = new Rectangle(r.Left, r.Top, r.Width, r.Height);
                using Mat roiImage = new Mat(frame, rect);

                /*Numberplate box detection*/
                using var keypoints = keyPointsDetector.Detect(roiImage);

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