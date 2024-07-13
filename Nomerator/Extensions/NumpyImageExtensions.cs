using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using NumpyDotNet;

namespace Nomerator
{
    public static class NumpyImageExtentions
    {
        public static ndarray LoadRgbImage(this string filename)
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

    }
}