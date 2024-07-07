
using System;
using Emgu.CV;
using NumpyDotNet;

namespace Nomerator
{
    public class ImageResizeOutput : IDisposable
    {
        private bool disposed = false;

        public ndarray Image { get; set; }

        public float[] Input { get; set; }

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

            this.disposed = true;
        }
    }
}

