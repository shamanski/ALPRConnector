
using System;
using Emgu.CV;
using Numpy;

namespace Nomerator
{
    public class ImageResizeOutput : IDisposable
    {
        private bool disposed = false;

        public NDarray Image { get; set; }

        public float Ratio { get; set; }

        public Mat OutputImageMat { get; set; }

        ~ImageResizeOutput()
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

            if (this.Image != null)
            {
                this.Image.Dispose();
            }

            this.disposed = true;
        }
    }
}

