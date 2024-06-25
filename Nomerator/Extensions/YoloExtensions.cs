
using Emgu.CV;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

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

        public static SKBitmap PreprocessSourceImage(SKBitmap sourceImage)
        {
            int RequiredWidth = 640;
            int RequiredHeight = 640;
            var (w, h) = (sourceImage.Width, sourceImage.Height); // image width and height
            var (xRatio, yRatio) = (RequiredWidth / (float)w, RequiredHeight / (float)h); // x, y ratios
            var ratio = Math.Min(xRatio, yRatio); // ratio = resized / original
            var (width, height) = ((int)(w * ratio), (int)(h * ratio)); // roi width and height
            var (x, y) = ((RequiredWidth / 2) - (width / 2), (RequiredHeight / 2) - (height / 2)); // roi x and y coordinates

            var roi = SKRectI.Create(new SKPointI(x, y), new SKSizeI(width, height));

            SKBitmap graph = new SKBitmap(RequiredWidth, RequiredHeight);
            using SKCanvas canvas = new SKCanvas(graph);
            canvas.DrawBitmap(sourceImage, roi);

            return graph;
        }

        public static Tensor<float> GetTensorForSKImage(SKBitmap image)
        {
            var bytes = image.GetPixelSpan();
            var expectedOutputLength = image.Width * image.Height * 3;
            float[] channelData = new float[expectedOutputLength];

            var expectedChannelLength = expectedOutputLength / 3;
            var greenOffset = expectedChannelLength;
            var blueOffset = expectedChannelLength * 2;

            for (int i = 0, i2 = 0; i < bytes.Length; i += 4, i2++)
            {
                var b = Convert.ToSingle(bytes[i]);
                var g = Convert.ToSingle(bytes[i + 1]);
                var r = Convert.ToSingle(bytes[i + 2]);
                channelData[i2] = (r) / 255.0f;
                channelData[i2 + greenOffset] = (g) / 255.0f;
                channelData[i2 + blueOffset] = (b) / 255.0f;
            }

            return new DenseTensor<float>(new Memory<float>(channelData), new[] { 1, 3, image.Height, image.Width });
        }

        public static SKBitmap ResizeImage(SKBitmap image, int targetWidth, int targetHeight)
        {
            var resized = new SKBitmap(targetWidth, targetHeight, image.ColorType, image.AlphaType);
            using (var canvas = new SKCanvas(resized))
            {
                canvas.Clear(SKColors.Transparent);
                var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.High,
                    IsAntialias = true
                };
                canvas.DrawBitmap(image, SKRect.Create(0, 0, targetWidth, targetHeight), paint);
            }
            return resized;
        }



        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}

