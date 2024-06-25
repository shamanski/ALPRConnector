using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Numpy;

namespace Nomerator
{
    public static class NumpyImageExtentions
    {
        public static NDarray LoadRgbImage(this string filename)
        {
            using var imageMat = new Mat(filename, loadType: ImreadModes.Color);

            using var rgbImage = new Mat();
            CvInvoke.CvtColor(imageMat, rgbImage, ColorConversion.Bgr2Rgb);

            return rgbImage.ToImageNDarray<byte>();
        }

        public static PointF[] AdjustResultCoordinates(this PointF[] polys, float ratio_w, float ratio_h, float ratio_net = 2)
        {
            if (polys.Length > 0)
            {
                for (int k = 0; k < polys.Length; k++)
                {
                    polys[k].X *= ratio_w * ratio_net;
                    polys[k].Y *= ratio_h * ratio_net;
                }
            }

            return polys;
        }

        public static NDarray NormalizeMeanVariance(this NDarray in_img, NDarray mean, NDarray variance)
        {
            // should be RGB order
            var img = in_img.copy().astype(np.float32);

            img -= np.array(new NDarray[] { mean[0] * 255.0, mean[1] * 255.0, mean[2] * 255.0 }, dtype: np.float32);
            img /= np.array(new NDarray[] { variance[0] * 255.0, variance[1] * 255.0, variance[2] * 255.0 }, dtype: np.float32);

            return img;
        }

        public static Mat Cvt2HeatmapImg(this NDarray img)
        {
            img = (np.clip(img, (NDarray)0, (NDarray)1) * 255).astype(np.uint8);
            var matImg = new Mat();
            CvInvoke.ApplyColorMap(img.ToMatImage<byte>(), matImg, ColorMapType.Jet);
            return matImg;
        }

        public static ImageResizeOutput ResizeAspectRatio(this NDarray img, float width_target, float height_target, Inter interpolation, float mag_ratio = 1)
        {
            var height = img.shape[0];
            var width = img.shape[1];
            var channel = img.shape[2];

            // Вычисляем масштабирующий коэффициент для увеличения изображения
            var ratio_w = width_target / (float)width;
            var ratio_h = height_target / (float)height;
            var ratio = Math.Min(ratio_w, ratio_h);

            // Изменяем размер изображения с помощью указанной интерполяции
            using var imgMat = new Mat();
            CvInvoke.Resize(img.ToMatImage<byte>(), imgMat, new Size((int)(width * ratio), (int)(height * ratio)), interpolation: interpolation);

            // Определяем размеры добавляемых отступов
            int top = 0, bottom = 0, left = 0, right = 0;

            if (imgMat.Width != width || imgMat.Height != height)
            {
                // Если изображение уменьшено по одной из размерностей, добавляем отступы
                var newWidth = (int)(width * ratio);
                var newHeight = (int)(height * ratio);

                if (newWidth < width_target)
                {
                    // Вычисляем количество пикселей для отступа слева
                    left = (int)((width_target - newWidth) / 2);
                    right = (int)(width_target - newWidth - left);
                }

                if (newHeight < height_target)
                {
                    // Вычисляем количество пикселей для отступа сверху
                    top = (int)((height_target - newHeight) / 2);
                    bottom = (int)(height_target - newHeight - top);
                }
            }

            // Добавляем отступы, если они требуются
            if (top > 0 || bottom > 0 || left > 0 || right > 0)
            {
                var paddedImageMat = new Mat();
                CvInvoke.CopyMakeBorder(imgMat, paddedImageMat, top, bottom, left, right, BorderType.Constant, new MCvScalar(0, 0, 0));

                // Преобразуем Mat обратно в NDarray
                var paddedImage = paddedImageMat.ToImageNDarray<byte>((int)height_target, (int)width_target, channel);

                // Возвращаем результат вместе с информацией о добавленных отступах
                return new ImageResizeOutput
                {
                    Image = paddedImage,
                    Ratio = ratio,
                    OutputImageMat = paddedImageMat.Clone()
                };
            }
            else
            {
                // Если отступы не добавлялись, просто возвращаем измененное изображение
                return new ImageResizeOutput
                {
                    Image = imgMat.ToImageNDarray<byte>(),
                    Ratio = ratio,
                   // AddedPadding = null
                };
            }
        }

    }
}