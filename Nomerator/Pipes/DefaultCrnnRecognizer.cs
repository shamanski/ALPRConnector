using System.Drawing;
using Emgu.CV;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;

namespace Nomerator
{
    public class DefaultCrnnTextRecognizer : IDisposable
    {
        private bool disposed = false;
        private readonly TextRecognitionModel model;
        private readonly Size inputSize;

        public DefaultCrnnTextRecognizer(string modelFile = "models/efficientnet_ocr.onnx", string vocabularyFile = "models/alphabet_36.txt") 
        {
            inputSize = new Size(200, 50);
            model = new TextRecognitionModel(modelFile);

            model.Vocabulary = File.ReadAllText(vocabularyFile).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            model.DecodeType = "CTC-greedy";
            
            model.SetInputScale(1.0 / 255.0 );
            model.SetInputMean(new MCvScalar(0, 0, 0));
            model.SetInputSize(this.inputSize);
        }

        ~DefaultCrnnTextRecognizer()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            this.model.Dispose();
            this.disposed = true;
        }

        public string Recognize(Mat image, PointF[] box)
        {
            var targetVertices = new PointF[]
            {
                new PointF(0, 0),
                new PointF(this.inputSize.Width - 1, 0),
                new PointF(this.inputSize.Width - 1, this.inputSize.Height - 1),
                new PointF(0, this.inputSize.Height - 1),
            };
            using var rotationMatrix = CvInvoke.GetPerspectiveTransform(box, targetVertices);
            using var cropped = new Mat();

            CvInvoke.WarpPerspective(image, cropped, rotationMatrix, this.inputSize);
            var result = model.Recognize(cropped)?.ToUpper();
            return result?.Replace("\r", string.Empty);
        }
       
    }
}
