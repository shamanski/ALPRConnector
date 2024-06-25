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

namespace Nomerator
{
    public class DetectionAndReading : IDisposable
    {
        private YoloV8Predictor localizationDetector;
        private CraftDetector keyPointsDetector;
        private DefaultCrnnTextRecognizer ocrDetector;
        private bool disposed = false;
        private Mat rgbMat;
        private Mat _dstBuffer;
        private Size cropSize;
        private double ratio;

        public DetectionAndReading()
        {
            localizationDetector = YoloV8Predictor.Create("model.onnx");
            keyPointsDetector = new CraftDetector("craft-var.onnx");
            ocrDetector = new DefaultCrnnTextRecognizer("CRNN_VGG_BiLSTM_CTC.onnx");
            rgbMat = new Mat();
            _dstBuffer = new Mat();
            ratio = 1.0;
        }

        public List<string> Recognize(Mat frame)
        {
            if (cropSize.IsEmpty)
            {
                if (frame.Width > 600)
                {
                    ratio = frame.Width / 600;
                    cropSize.Width = 600;
                    cropSize.Height = (int)(frame.Height / ratio);
                }
                else
                {
                    cropSize.Width = frame.Width; 
                    cropSize.Height = frame.Height;
                }
            }
            
            CvInvoke.Resize(frame, _dstBuffer, cropSize);
            var result = localizationDetector.Detect(frame.ToTensor(), 1.0);
            var plates = new List<string>();
            foreach (var entry in result.Boxes)
            {
                var r = entry.Bounds;
                Rectangle rect = new Rectangle(r.Left, r.Top, r.Width, r.Height);
                using Mat roiImage = new Mat(frame, rect);
                //CvInvoke.Imwrite("cr.jpg", roiImage);
                var keypoints = keyPointsDetector.Detect(roiImage);
                var plate = new StringBuilder();
                foreach (var idx in keypoints.Boxes.Keys)
                {
                    var points = keypoints.Boxes[idx].Select(x => new System.Drawing.PointF(x.X * 1, x.Y * 1)).ToArray();
                    using var toOcr = keypoints.OutputImage.Clone();
                    var textBlock = ocrDetector.Recognize(toOcr, points);
                    plate.Append(textBlock);
                }
                plates.Add(plate.ToString());
            }

            return plates;
        }

        private  unsafe Image<Rgb24> MatToImageSharp(Mat mat)
        {           
            CvInvoke.CvtColor(mat, rgbMat, ColorConversion.Bgr2Rgb);

            byte* dataPtr = (byte*)rgbMat.DataPointer;

            int width = rgbMat.Width;
            int height = rgbMat.Height;
            int stride = rgbMat.Step;
            Memory<Rgb24> memory = new Memory<Rgb24>(new Rgb24[width * height]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte* pixel = dataPtr + y * stride + x * 3;
                    memory.Span[y * width + x] = new Rgb24(pixel[0], pixel[1], pixel[2]);
                }
            }

            var imageSharp = Image.WrapMemory(memory, width, height);


            return imageSharp;
        }
    

        /*  private static Image<Rgb24> MatToImageSharp(Mat mat)
          {
              var data = mat.DataPointer;
              var imageSharp = Image.WrapMemory<Rgb24>(data, mat.Width, mat.Height);
              using Image<Rgb, byte> afterImage = mat.ToImage<Rgb, byte>();
              byte[] afterbytes = afterImage.Bytes;
              return Image.LoadPixelData<Rgb24>(afterbytes, mat.Width, mat.Height);            
          }*/

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