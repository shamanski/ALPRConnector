using System;
using System.Drawing;
using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Reg;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Nomerator
{
    public class DefaultCrnnTextRecognizer : IDisposable
    {
        private bool disposed = false;

        private readonly TextRecognitionModel model;

        private readonly Size inputSize;

        public DefaultCrnnTextRecognizer(string modelFile = "c:/1/attempt4.onnx", string vocabularyFile = "alphabet_36.txt") //CRNN_VGG_BiLSTM_CTC.
        {
            inputSize = new Size(200, 50);
            model = new TextRecognitionModel("c:/1/attempt4.onnx");

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

            //CvInvoke.Imwrite("ready-to-ocr.jpg", cropped);
            var result = model.Recognize(cropped).ToUpper();
            return result?.Replace("\r", string.Empty);
        }


        void test()
        {
            /* var inputTensor = new DenseTensor<float>(new[] { 1, 3, 50, 200 });
             var normalizedMat = new Mat();
             //CvInvoke.Normalize(cropped, normalizedMat, 0, 1, NormType.MinMax, DepthType.Cv32F);
             for (int y = 0; y < cropped.Rows; y++)
             {
                 for (int x = 0; x < cropped.Cols; x++)
                 {
                     var pixel = cropped.GetRawData(y, x);
                     inputTensor[0, 0, y, x] = pixel[2] / 255f; // Красный канал
                     inputTensor[0, 1, y, x] = pixel[1] / 255f; // Зеленый канал
                     inputTensor[0, 2, y, x] = pixel[0] / 255f; // Синий канал
                 }
             }
             var session = new InferenceSession("c:/1/attempt4.onnx");

             var inputMeta = session.InputMetadata;
             var inputs = new List<NamedOnnxValue>
             {
                 NamedOnnxValue.CreateFromTensor(inputMeta.Keys.First(), inputTensor)
             };
             using var results = session.Run(inputs);
             var output = results.First().AsTensor<float>().ToArray();
             int timeSteps = 13;
             int numClasses = 37;

             // Преобразование выходного тензора в формат [timeSteps, numClasses]
             float[,,] outputTensor = new float[timeSteps, 1, numClasses];
             Buffer.BlockCopy(output, 0, outputTensor, 0, output.Length * sizeof(float));

             // Алфавит символов
             char[] alphabet = "_0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

             // Интерпретация выходного тензора
             string resultx = "";
             for (int t = 0; t < timeSteps; t++)
             {
                 float[] probabilities = new float[numClasses];
                 for (int c = 0; c < numClasses; c++)
                 {
                     probabilities[c] = outputTensor[t, 0, c];
                 }

                 // Нахождение символа с максимальной вероятностью
                 int maxIndex = Array.IndexOf(probabilities, probabilities.Max());
                 if (maxIndex < alphabet.Length)
                 {
                     resultx += alphabet[maxIndex];
                 }
             }*/
        }
    }
}
