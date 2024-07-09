using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using MathNet.Numerics.LinearAlgebra;
using NumpyDotNet;
using System.Diagnostics;
using Serilog;

namespace Nomerator
{
    public class CraftDetector : IDisposable
    {
        private readonly string modelFile;
        private readonly MLContext mlContext;
        private readonly PredictionEngine<CraftInput, CraftOutput> predictEngine;
        private InferenceSession _session;
        private RunOptions _runOptions;
        private bool disposed = false;
        private readonly int _modelImageWidth;
        private readonly int _modelImageHeight;

        public CraftDetector(string modelFile = "attempt-craft1.onnx", int modelImageWidth = 320, int modelImageHeight = 96)
        {
            this.modelFile = modelFile;
            mlContext = new MLContext();
            var dataView = mlContext.Data.LoadFromEnumerable(new List<CraftInput>());
            
            var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    modelFile: this.modelFile,
                    outputColumnNames: new[] {
                               "285", "onnx::Conv_275"},
                    inputColumnNames: new[] {
                               "input.1"});

            var mlNetModel = pipeline.Fit(dataView);

            predictEngine = mlContext.Model.CreatePredictionEngine<CraftInput, CraftOutput>(mlNetModel);
            var opts = new SessionOptions()
            {
                IntraOpNumThreads = 2,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
            _session = new InferenceSession("attempt-refiner1.onnx", opts);
            _runOptions = new RunOptions();
            _modelImageHeight = modelImageHeight; 
            _modelImageWidth = modelImageWidth;
        }

        public DetectionResult Detect(Mat image, float lowText = 0.4f, float textThreshold = 0.6f, float linkThreshold = 0.6f)
        {
            var img_h = _modelImageHeight / 2;
            var img_w = _modelImageWidth / 2;
            var xx = image.ToInput();         
            var result = this.predictEngine.Predict(new CraftInput() { Image = xx.Input });
            
            using var input_y = OrtValue.CreateTensorValueFromMemory<float>(OrtMemoryInfo.DefaultInstance,
                result.Output, [1, img_h, img_w, 2]);
            using var input_feature = OrtValue.CreateTensorValueFromMemory<float>(OrtMemoryInfo.DefaultInstance,
                result.feature, [1, 32, img_h, img_w]);
            var inputsRefiner = new Dictionary<string, OrtValue>
            {
                {
                    "onnx::Transpose_0", input_y
                },
                {
                    "onnx::Concat_1", input_feature
                }
            };
            
            using IDisposableReadOnlyCollection<OrtValue> refinerResults = _session.Run(_runOptions, inputsRefiner, _session.OutputNames);
            var outputSize = new Size(img_w, img_h);
            using var textmap = new Mat(outputSize, DepthType.Cv32F, 1);         
            using var linkmap = new Mat(outputSize, DepthType.Cv32F, 1);
            FillMatFromArray3D(textmap, result.Output);
            linkmap.SetTo<float>(refinerResults[0].GetTensorDataAsSpan<float>().ToArray());
            

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
                
                var np_contours = tempArr.Transpose(new long[] { 1, 0 }).reshape(-1, 2);

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
                    var l = (float)np.min(np_contours[":,0"]);
                    var r = (float)np.max(np_contours[":,0"]);
                    var t = (float)np.min(np_contours[":,1"]);
                    var b = (float)np.max(np_contours[":,1"]);
                    box = np.array(new float[,] { { l, t }, { r, t }, { r, b }, { l, b } }, dtype: np.Float32);
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
                    predictEngine.Dispose();
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
