using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Rapid;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime.Tensors;

//using Numpy;using Numpy;

//using Numpy;

//using Numpy.Models;
using NumpyDotNet;

namespace Nomerator
{
    public static class NumpyOpenCvExtentions
    {
        public static Mat ToMatImage<T>(this ndarray npArrary)
        {
            var result = new Mat((int)npArrary.shape[0], (int)npArrary.shape[1], GetDepthType<T>(), npArrary.shape.iDims.Length == 3 ? (int)npArrary.shape[2] : 1);

            switch ( typeof(T).Name )
            {
                case nameof(Single):
                    {
                        result.SetTo<float>(npArrary.AsFloatArray());
                        return result;
                    }

                case nameof(Byte):
                    {
                        result.SetTo<byte>(npArrary.AsByteArray());
                        return result;
                    }
                default: 
                    {
                        throw new ArgumentException(typeof(T).Name);
                    }
            }

            
        }


        public static ImageResizeOutput ToInput(this Mat mat, int targetWidth = 512, int targetHeight = 384)
        {
            double aspectRatio = Math.Min((double)targetWidth / mat.Width, (double)targetHeight / mat.Height);
            int newWidth = (int)(mat.Width * aspectRatio);
            int newHeight = (int)(mat.Height * aspectRatio);

            using Mat resizedMat = new Mat();
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



        public static ndarray ToImageNDarray<T>(this Mat mat)
        {
            return ToImageNDarray<T>(mat, mat.Cols, mat.Rows, mat.NumberOfChannels);
        }

        public static ndarray ToImageNDarray(this Mat mat, int width, int height, int channels)
        {
            return ToImageNDarray<float>(mat, width, height, channels);
        }

        public static ndarray ToImageNDarray<T>(this Mat mat, int width, int height, int channels)
        {
            var data = new T[height * width * mat.NumberOfChannels];
            mat.CopyTo<T>(data);
            var shape = mat.NumberOfChannels > 1 ? new shape(height, width, mat.NumberOfChannels) : new shape(height, width);
            return np.reshape(np.array(data), shape);
        }

        public static PointF[] ToPointsArray(this ndarray arr)
        {
            int length = (int)arr.shape[0];
            PointF[] points = new PointF[length];

            for (int i = 0; i < length; i++)
            {
                points[i] = new PointF((float)arr[i, 0], (float)arr[i, 1]);
            }

            return points;
        }

        public static ndarray FromPointsArray(this PointF[] points)
        {
            var result = np.zeros(new shape(points.Length, 2), np.Float32);
            for (var i = 0; i < points.Length; i++)
            {
                result[i, 0] = points[i].X;
                result[i, 1] = points[i].Y;
            }

            return result;
        }

        public static ndarray WhereFlags<T>(this ndarray input, ndarray flags, Func<bool, T, T> func)
        {
            var result = new List<T>();
            var data = GetArrayData<T>(input);

            var flagsValues = flags.AsBoolArray();

            for (var i = 0; i < data.Length; i++)
            {
                result.Add(func(flagsValues[i], data[i]));
            }

            var flat =  np.array(result.ToArray());

            return np.reshape(flat, input.shape);
        }

        public static T[] GetArrayData<T>(ndarray input) 
        {
            if (typeof(T) == typeof(float))
            {
                return input.AsFloatArray() as T[];
            }
            else if (typeof(T) == typeof(byte))
            {
                return input.AsByteArray() as T[];
            }
            else if (typeof(T) == typeof(bool))
            {
                return input.AsBoolArray() as T[];
            }
            else
            {
                throw new ArgumentException($"Unsupported type: {typeof(T).Name}");
            }
        }

        public static Mat WhereFlags<T>(this Mat input, Mat flags, Func<bool, T, T> func) where T : struct
        {
            if (input.Size != flags.Size || input.Depth != flags.Depth || flags.Depth != DepthType.Cv8U)
            {
                throw new ArgumentException("Input and flags Mat must have the same size and depth, and flags must be of type CV_8U.");
            }

            T[] inputData = new T[input.Width*input.Height];
            input.CopyTo<T>(inputData);
            var flagsData = flags.GetRawData();

            var result = new T[input.Rows * input.Cols];

            for (var i = 0; i < result.Length; i++)
            {
                bool flag = flagsData[i] != 0;
                T data = (T)Convert.ChangeType(inputData[i], typeof(T));
                result[i] = func(flag, data);
            }

            var resultMat = new Mat(input.Rows, input.Cols, input.Depth, 1);

            // Fill the result Mat with the processed data
            //Marshal.Copy(result.Cast<object>().ToArray(), 0, resultMat.DataPointer, result.Length);

            return resultMat;
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