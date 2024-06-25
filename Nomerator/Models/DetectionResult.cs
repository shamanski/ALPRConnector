using System.Drawing;
using Emgu.CV;
using Numpy;

namespace Nomerator
{
    public class DetectionResult : IDisposable
    {
        private bool disposed = false;

        public Mat OutputImage { get; set; }

        public Dictionary<int, PointF[]> Boxes { get; set; }

        public NDarray ScoreText { get; set; }

        public NDarray ScoreLink { get; set; }

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

            if (this.ScoreText != null)
            {
                this.ScoreText.Dispose();
            }

            if (this.ScoreLink != null)
            {
                this.ScoreLink.Dispose();
            }

            this.disposed = true;
        }
    }
}
