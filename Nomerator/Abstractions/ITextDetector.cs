using System;
using System.Drawing;
using Emgu.CV;

namespace Nomerator
{
    public interface ITextDetector : IDisposable
    {
        DetectionResult Detect(Bitmap bitmap);

        DetectionResult Detect(Mat imageMat);
    }
}