using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using MathNet.Numerics.LinearAlgebra;
using NumpyDotNet;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;
using Serilog;
namespace Nomerator
{
    public class CraftDetector : IDisposable
    {
        private readonly string modelFile;
        private InferenceSession _session;
        private InferenceSession _refinerSession;
        private RunOptions _runOptions;
        private bool disposed = false;
        private readonly int _modelImageWidth;
        private readonly int _modelImageHeight;

        private readonly byte[] _byteArrayBuffer;
        private readonly float[] _bufferInput;
        private readonly float[] _bufferOutputY; 
        private readonly float[] _bufferOutputFeature;
        private readonly float[] _bufferOutputRefiner;

        private DenseTensor<float> _tensorInput;
        private DenseTensor<float> _tensorOutputY;
        private DenseTensor<float> _tensorOutputFeature;
        private DenseTensor<float> _tensorOutputRefiner;

        private FixedBufferOnnxValue _valueInput;
        private FixedBufferOnnxValue _valueOutputY;
        private FixedBufferOnnxValue _valueOutputFeature;
        private FixedBufferOnnxValue _valueOutputRefiner;

        private readonly string[] _inputNames;
        private readonly string[] _outputNames;
        private readonly string[] _inputRefinerNames;
        private readonly string[] _outputRefinerNames;
       
        private readonly IReadOnlyCollection<FixedBufferOnnxValue> _inputValues;
        private readonly IReadOnlyCollection<FixedBufferOnnxValue> _outputValues;
        private readonly IReadOnlyCollection<FixedBufferOnnxValue> _inputRefinerValues;
        private readonly IReadOnlyCollection<FixedBufferOnnxValue> _outputRefinerValues;

        Stopwatch sw = new Stopwatch();

        public CraftDetector(string modelFile = "models/craft.onnx", int modelImageWidth = 320, int modelImageHeight = 96)
        {
            _modelImageHeight = modelImageHeight;
            _modelImageWidth = modelImageWidth;
            var img_h = _modelImageHeight / 2;
            var img_w = _modelImageWidth / 2;

            this.modelFile = modelFile;

            _bufferInput = new float[modelImageWidth * modelImageHeight * 3];
            _byteArrayBuffer = new byte[modelImageWidth * modelImageHeight * 3];
            _bufferOutputY = new float[img_h * img_w * 2];
            _bufferOutputFeature = new float[img_h * img_w * 32];
            _bufferOutputRefiner = new float[img_h * img_w];

            _tensorInput = new DenseTensor<float>(_bufferInput, [ 1, 3, modelImageHeight, modelImageWidth]);
            _tensorOutputY = new DenseTensor<float>(_bufferOutputY, [ 1, img_h, img_w, 2 ]);
            _tensorOutputFeature = new DenseTensor<float>(_bufferOutputFeature, [ 1, 32, img_h, img_w ]);
            _tensorOutputRefiner = new DenseTensor<float>(_bufferOutputRefiner, [1, img_h, img_w, 1]);

            _valueInput = FixedBufferOnnxValue.CreateFromTensor(_tensorInput);
            _valueOutputY = FixedBufferOnnxValue.CreateFromTensor(_tensorOutputY);
            _valueOutputFeature = FixedBufferOnnxValue.CreateFromTensor(_tensorOutputFeature);
            _valueOutputRefiner = FixedBufferOnnxValue.CreateFromTensor(_tensorOutputRefiner);

            _inputNames = ["input.1"];
            _outputNames = ["285", "onnx::Conv_275"];
            _inputRefinerNames = ["onnx::Transpose_0", "onnx::Concat_1"];
            _outputRefinerNames = ["129"];
            
            _inputValues = [ _valueInput ];
            _outputValues = [ _valueOutputY, _valueOutputFeature ];
            _inputRefinerValues = [_valueOutputY, _valueOutputFeature];
            _outputRefinerValues = [_valueOutputRefiner];

            

            var opts = new SessionOptions()
            {
                IntraOpNumThreads = 2,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
            _session = new InferenceSession("models/craft.onnx", opts);
            _refinerSession = new InferenceSession("models/refiner.onnx", opts);
            _runOptions = new RunOptions();
        }

        public DetectionResult Detect(Mat image, float lowText = 0.4f, float textThreshold = 0.6f, float linkThreshold = 0.6f)
        {
            var img_h = _modelImageHeight / 2;
            var img_w = _modelImageWidth / 2;
            var xx = image.ToInput();         

            PrepareTensor(image);
            
            _session.Run(_inputNames, _inputValues, _outputNames, _outputValues);
            _refinerSession.Run(_inputRefinerNames, _inputRefinerValues, _outputRefinerNames, _outputRefinerValues);
            sw.Stop();
            var outputSize = new Size(img_w, img_h);
            using var textmap = new Mat(outputSize, DepthType.Cv32F, 1);         
            using var linkmap = new Mat(outputSize, DepthType.Cv32F, 1);
            FillMatFromArray3D(textmap, _bufferOutputY);
            linkmap.SetTo<float>(_bufferOutputRefiner);            

            using Mat textScoreThresholded = new Mat();
            using Mat textScoreThresholded2 = new Mat();
            CvInvoke.Threshold(textmap, textScoreThresholded, lowText, 1, ThresholdType.Binary);
            using Mat linkScoreThresholded = new Mat();
            using Mat linkScoreThresholded2 = new Mat();
            CvInvoke.Threshold(linkmap, linkScoreThresholded, linkThreshold, 1, ThresholdType.Binary);
            var scoreText = textScoreThresholded.ToImageNDarray<float>();
            var scoreLink = linkScoreThresholded.ToImageNDarray<float>();

            var text_score_comb = np.clip(scoreText + scoreLink, 0, 1);

            var text_score_comb_bytes = text_score_comb.astype(np.UInt8);

            using var labelsMat = new Mat();
            using var statsMat = new Mat();
            using var centroidsMat = new Mat();
            var text_score_comb_data = text_score_comb_bytes.ToMatImage<byte>();
            var nLabels = CvInvoke.ConnectedComponentsWithStats(text_score_comb_data, labelsMat, statsMat, centroidsMat, connectivity: LineType.FourConnected);

            var stats = statsMat.ToImageNDarray<int>();
            var labels = labelsMat.ToImageNDarray<int>();
            var centroids = centroidsMat.ToImageNDarray<float>();
            var boxes = new Dictionary<int, PointF[]>();
            var allPoints = new List<PointF>();
            

            for (var k = 1; k < nLabels; k++)
            {
                // size filtering
                var size = (int)stats[k, (int)ConnectedComponentsTypes.Area];
                if (size < 200)
                {
                    continue;
                }

                var labelFlags = labels ==  k;
                var textMapArr = textmap.ToImageNDarray<float>().WhereFlags<float>(labelFlags, (flag, elem) => flag ? elem : 0.0f);

      
                if ((float)np.max(textMapArr) < textThreshold)
                {
                   continue;
                }

                // make segmentation map
                var segmapZero = np.zeros(new shape(img_h, img_w), dtype: np.UInt8);
                var segmap1 = segmapZero.WhereFlags<byte>(labelFlags, (flag, elem) => (byte)(flag ? 255 : 0));
                var segmap = segmap1.WhereFlags<byte>(np.logical_and(scoreLink ==1, scoreText ==0 ), (flag, elem) => (byte)(flag ? 0 : elem));

                var x = (int)stats[k, (int)ConnectedComponentsTypes.Left];
                var y = (int)stats[k, (int)ConnectedComponentsTypes.Top];
                var w = (int)stats[k, (int)ConnectedComponentsTypes.Width];
                var h = (int)stats[k, (int)ConnectedComponentsTypes.Height];

                var niter = (int)(Math.Sqrt(size * Math.Min(w, h) / (w * h)) * 2);
                var sx = x - niter;
                var ex = x + w + niter + 1;
                var sy = y - niter;
                var ey = y + h + niter + 1;
               
                // boundary check
                if (sx < 0) sx = 0;
                if (sy < 0) sy = 0;
                if (ex >= img_w) ex = img_w;
                if (ey >= img_h) ey = img_h;

                using var kernelMat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1 + niter, 1 + niter), new Point(-1, -1));
                using var dilateMat = new Mat();
                var segmapBounds = segmap.A(new Slice(sy, ey), new Slice(sx, ex));
                using var segmapMat = segmapBounds.ToMatImage<byte>();
                CvInvoke.Dilate(segmapMat, dilateMat, kernelMat, new Point(-1, -1), -1, BorderType.Default, new MCvScalar());
                
                ndarray dilate = dilateMat.ToImageNDarray<byte>();
                
                

                segmap[new Slice(sy, ey), new Slice(sx, ex)] = dilate;

                
                // make box
                var tempArr = np.roll(np.array(np.where(segmap !=0)),  1 , axis: 0);
                
                var np_contours = tempArr.Transpose([ 1, 0 ]).reshape(-1, 2);

                var rectangle = CvInvoke.MinAreaRect(np_contours.ToMatImage<float>());

                var boxPoints = CvInvoke.BoxPoints(rectangle);
                
                var box = boxPoints.FromPointsArray();
                
                allPoints.AddRange(boxPoints);

                // align diamond-shape
                var v0 = Vector<float>.Build.DenseOfArray([(float)box[0, 0], (float)box[0, 1]]);
                var v1 = Vector<float>.Build.DenseOfArray([(float)box[1, 0], (float)box[1, 1]]);
                var v2 = Vector<float>.Build.DenseOfArray([(float)box[2, 0], (float)box[2, 1]]);
                float boxW = (float)v0.Subtract(v1).L2Norm();
                float boxH = (float)v1.Subtract(v2).L2Norm();
                var box_ratio = Math.Max(boxW, boxH) / (Math.Min(boxW, boxH) + 1e-5);
                if (Math.Abs(1 - box_ratio) <= 0.2)
                {
                    continue;
                }

                // make clock-wise order
                var startidx = (int)(long)box.Sum(axis: 1).ArgMin(0);
                box = np.roll(box,  4 - startidx , axis: 0);
                box = np.array(box);
                boxes.Add(k, box.ToPointsFloatArray().AdjustResultCoordinates(1, 1));

            }
            
            return new DetectionResult
            {
                Boxes = boxes
                 .OrderBy(kvp => kvp.Value.Min(p => p.X))
                 .ToDictionary(),
                ScoreText = scoreText,
                ScoreLink = scoreLink,
                OutputImage =  xx.OutputImageMat
        };
        }

        private void PrepareTensor(Mat mat, int targetWidth = 320, int targetHeight = 96)
        {
            double aspectRatio = Math.Min((double)targetWidth / mat.Width, (double)targetHeight / mat.Height);
            int newWidth = (int)(mat.Width * aspectRatio);
            int newHeight = (int)(mat.Height * aspectRatio);

            using Mat resizedMat = new Mat();
            CvInvoke.Resize(mat, resizedMat, new Size(newWidth, newHeight), interpolation: Inter.Linear);

            var xPadding = (targetWidth - resizedMat.Width) / 2;
            var yPadding = (targetHeight - resizedMat.Height) / 2;

            var width = resizedMat.Width;
            var height = resizedMat.Height;
            var dimensions = new int[] { 1, 3, targetHeight, targetWidth };

            var strideBatchR = 0;
            var strideBatchG = targetHeight * targetWidth;
            var strideBatchB = targetHeight * targetWidth * 2;
            var strideY = 320;
            var strideX = 1;

            resizedMat.CopyTo(_byteArrayBuffer);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = (y * width + x) * 3; // Index in byteArrayBuffer (BGR)
                    int tensorIndex = strideBatchR + strideY * (y + yPadding) + strideX * (x + xPadding);


                    float r = _byteArrayBuffer[pixelIndex] / 255f; // R
                    float g = _byteArrayBuffer[pixelIndex + 1] / 255f; // G
                    float b = _byteArrayBuffer[pixelIndex + 2] / 255f; // B

                    // Apply mean subtraction and variance division
                    _bufferInput[tensorIndex] = (r - 0.485f) / 0.229f;
                    _bufferInput[tensorIndex + strideBatchG - strideBatchR] = (g - 0.456f) / 0.224f;
                    _bufferInput[tensorIndex + strideBatchB - strideBatchR] = (b - 0.406f) / 0.225f;
                }
            }
        }

        private void FillMatFromArray3D(Mat textmap, float[] array)
        {
            int totalElements = textmap.Rows * textmap.Cols;
            float[] textArray = new float[totalElements];

            for (int i = 0, textIndex = 0; i < array.Length; i += 2, textIndex++)
            {
                textArray[textIndex] = array[i];
            }
            textmap.SetTo<float>(textArray);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _refinerSession.Dispose();
                    _session.Dispose();
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
