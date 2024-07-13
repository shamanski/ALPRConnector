using Emgu.CV;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Nomerator
{
    public static class Utils
    {
        public static float[] Xywh2xyxy(float[] source)
        {
            var result = new float[4];

            result[0] = source[0] - source[2] / 2f;
            result[1] = source[1] - source[3] / 2f;
            result[2] = source[0] + source[2] / 2f;
            result[3] = source[1] + source[3] / 2f;

            return result;
        }

        public static Tensor<float> ToTensor(this Mat mat)
        {
            if (mat.NumberOfChannels != 3)
            {
                throw new ArgumentException("No such RGB channels");
            }

            int height = mat.Rows;
            int width = mat.Cols;
            int channels = mat.NumberOfChannels;

            byte[] data = new byte[height * width * channels];
            mat.CopyTo(data);

            float[] normalizedData = new float[height * width * channels];
            for (int i = 0; i < data.Length; i++)
            {
                normalizedData[i] = data[i] / 255.0f;
            }

            var tensor = new DenseTensor<float>(normalizedData, new[] { 1, channels, height, width });
            return tensor;
        }

        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}

