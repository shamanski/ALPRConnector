using System;
using System.Drawing;
using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;

namespace Nomerator
{
    public class DefaultCrnnTextRecognizer : IDisposable
    {
        private bool disposed = false;

        private readonly TextRecognitionModel model;

        private readonly Size inputSize;

        public DefaultCrnnTextRecognizer(string modelFile = "CRNN_VGG_BiLSTM_CTC.onnx", string vocabularyFile = "alphabet_36.txt")
        {
            this.inputSize = new Size(100, 32);
            this.model = new TextRecognitionModel(modelFile);

            this.model.Vocabulary = File.ReadAllText(vocabularyFile).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            this.model.DecodeType = "CTC-greedy";

            this.model.SetInputScale(1.0 / 127.5);
            this.model.SetInputMean(new MCvScalar(127.5, 127.5, 127.5));
            this.model.SetInputSize(this.inputSize);
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
            using var gray = new Mat();

            CvInvoke.WarpPerspective(image, cropped, rotationMatrix, this.inputSize);
            CvInvoke.CvtColor(cropped, gray, ColorConversion.Bgr2Gray);
            var result = model.Recognize(gray).ToUpper();

            return result?.Replace("\r", string.Empty);
        }
    }
}
