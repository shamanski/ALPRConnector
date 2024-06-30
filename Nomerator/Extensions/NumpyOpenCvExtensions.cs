using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Rapid;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime.Tensors;
using Numpy;
using Numpy.Models;

namespace Nomerator
{
    public static class NumpyOpenCvExtentions
    {
        public static Mat ToMatImage<T>(this NDarray npArrary)
        {
            var result = new Mat(npArrary.shape[0], npArrary.shape[1], GetDepthType<T>(), npArrary.shape.Dimensions.Length == 3 ? npArrary.shape[2] : 1);
            var arr = npArrary.GetData<T>();
            result.SetTo<T>(arr);
            return result;
        }


        public static ImageResizeOutput ToInput(this Mat mat, int targetWidth = 512, int targetHeight = 384)
        {
            double aspectRatio = Math.Min((double)targetWidth / mat.Width, (double)targetHeight / mat.Height);
            int newWidth = (int)(mat.Width * aspectRatio);
            int newHeight = (int)(mat.Height * aspectRatio);

            Mat resizedMat = new Mat();
            CvInvoke.Resize(mat, resizedMat, new Size(newWidth, newHeight), interpolation: Inter.Linear);

            var width = resizedMat.Width;
            var height = resizedMat.Height;

            int top = 0, bottom = 0, left = 0, right = 0;

            if (newWidth < targetWidth)
            {

                left = (int)((targetWidth - width) / 2);
                right = (int)(targetWidth - width - left);
            }

            if (newHeight < targetHeight)
            {

                top = (int)((targetHeight - height) / 2);
                bottom = (int)(targetHeight - height - top);
            }


            var paddedImageMat = new Mat();
            CvInvoke.CopyMakeBorder(resizedMat, paddedImageMat, top, bottom, left, right, BorderType.Constant, new MCvScalar(0, 0, 0));

            var dimensions = new int[] { 3, targetHeight, targetWidth }; // Change order to [color, height, width]
            var target = new DenseTensor<float>(dimensions);

            var stride = 196608;
            var buffer = new float[targetWidth * targetHeight * 3];
            var byteArrayBuffer = new byte[targetWidth * targetHeight * 3];
            Marshal.Copy(paddedImageMat.DataPointer, byteArrayBuffer, 0, byteArrayBuffer.Length);

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int pixelIndex = (y * targetWidth + x) * 3; // Index in byteArrayBuffer (BGR)
                    int targetIndex = y * targetWidth + x; // Index in buffer

                    float r = byteArrayBuffer[pixelIndex] / 255f; // R
                    float g = byteArrayBuffer[pixelIndex + 1] / 255f; // G
                    float b = byteArrayBuffer[pixelIndex + 2] / 255f; // B

                    // Apply mean subtraction and variance division
                    buffer[targetIndex] = (r - 0.485f) / 0.229f;
                    buffer[stride + targetIndex] = (g - 0.456f) / 0.224f;
                    buffer[stride * 2 + targetIndex] = (b - 0.406f) / 0.225f;
                }
            }
            return new ImageResizeOutput
            {
                OutputImageMat = paddedImageMat,
                Input = buffer
            }; 
        }



        public static NDarray ToImageNDarray<T>(this Mat mat)
        {
            return ToImageNDarray<T>(mat, mat.Cols, mat.Rows, mat.NumberOfChannels);
        }

        public static NDarray ToImageNDarray(this Mat mat, int width, int height, int channels)
        {
            return ToImageNDarray<float>(mat, width, height, channels);
        }

        public static NDarray ToImageNDarray<T>(this Mat mat, int width, int height, int channels)
        {
            var data = new T[height * width * mat.NumberOfChannels];
            mat.CopyTo<T>(data);
            return np.reshape(data, height, width, channels);
        }

        public static PointF[] ToPointsArray(this NDarray arr)
        {
            var points = new List<PointF>();
            for (var i = 0; i < arr.shape[0]; i++)
            {
                points.Add(new PointF((float)arr[i, 0], (float)arr[i, 1]));
            }

            return points.ToArray();
        }

        public static NDarray FromPointsArray(this PointF[] points)
        {
            var result = np.zeros(new Shape(points.Length, 2), np.float32);
            for (var i = 0; i < points.Length; i++)
            {
                result[i, 0] = (NDarray)points[i].X;
                result[i, 1] = (NDarray)points[i].Y;
            }

            return result;
        }

        public static NDarray WhereFlags<T>(this NDarray input, NDarray flags, Func<bool, T, T> func)
        {
            var result = new List<T>();
            var data = input.GetData<T>();
            var flagsValues = flags.GetData<bool>();

            for (var i = 0; i < data.Length; i++)
            {
                result.Add(func(flagsValues[i], data[i]));
            }

            using var flat = new NDarray<T>(result.ToArray());

            return np.reshape(flat, input.shape);
        }

        private static DepthType GetDepthType<T>()
        {
            if (typeof(T) == typeof(byte))
            {
                return DepthType.Cv8U;
            }
            else if (typeof(T) == typeof(float))
            {
                return DepthType.Cv32F;
            }

            return DepthType.Cv32F;
        }
    }
}