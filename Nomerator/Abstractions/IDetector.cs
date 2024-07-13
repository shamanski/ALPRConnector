using Emgu.CV;

namespace Nomerator.Abstractions
{
    internal interface IDetector: IDisposable
    {
        public List<YoloPrediction> Detect(Mat image, float conf_thres = 0, float iou_thres = 0);
    }
}
