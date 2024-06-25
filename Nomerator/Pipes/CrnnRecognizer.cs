using System;
using System.Drawing;
using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Microsoft.ML;
using SixLabors.ImageSharp.Metadata;


namespace Nomerator
{
    public class CrnnTextRecognizer : IDisposable
    {
        private bool disposed = false;

        private readonly TextRecognitionModel model;

        private readonly Size inputSize;

        public CrnnTextRecognizer(string modelFile = "ocr.onnx", string vocabularyFile = "alphabet_36.txt")
        {
            this.inputSize = new Size(400, 100);
            this.model = new TextRecognitionModel(modelFile);

            this.model.Vocabulary = File.ReadAllText(vocabularyFile).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            this.model.DecodeType = "CTC-greedy";
            this.model.SetInputScale(1.0 / 255.0);
            this.model.SetInputMean(new MCvScalar(0.0, 0.0, 0.0));
            this.model.SetInputSize(this.inputSize);
        }

        ~CrnnTextRecognizer()
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

        public string Recognize(Image<Gray, byte> image, PointF[] box)
        {
            return Recognize(image.Mat, box);
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

            var cropped = new Mat();
            CvInvoke.WarpPerspective(image, cropped, rotationMatrix, this.inputSize);
            CvInvoke.Imwrite("ready-to-ocr.jpg", cropped);
            var _ocr = new Tesseract("C:\\1\\", "eng", OcrEngineMode.TesseractLstmCombined);
            _ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890");
            _ocr.SetImage(cropped.Clone());
            _ocr.Recognize();
            var rr = _ocr.GetUTF8Text();
            var r = DnnInvoke.ReadNetFromONNX("ocr.onnx");
            var blob = DnnInvoke.BlobFromImage(cropped,1 / 255.0, new Size(400, 100), new MCvScalar(0, 0, 0));
            r.SetInput(blob);
            var o = r.Forward();
            var result = model.Recognize(cropped);

            return result.Replace("\r", string.Empty);
        }
    }
}