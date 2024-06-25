using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomerator.Abstractions
{
    internal interface IDetector: IDisposable
    {
        public List<YoloPrediction> Detect(SKBitmap image, float conf_thres = 0, float iou_thres = 0);
    }
}
