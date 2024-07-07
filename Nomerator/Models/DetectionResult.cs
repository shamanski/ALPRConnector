using System.Drawing;
using Emgu.CV;
using NumpyDotNet;

namespace Nomerator
{
    public class DetectionResult : IDisposable
    {
        private bool disposed = false;

        public Mat OutputImage { get; set; }

        public Dictionary<int, PointF[]> Boxes { get; set; }

        public ndarray ScoreText { get; set; }

        public ndarray ScoreLink { get; set; }

        ~DetectionResult()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }           

            this.disposed = true;
        }
    }
}
