
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Compunet.YoloV8.Utilities;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MathNet.Numerics.LinearAlgebra;
using NumpyDotNet;

namespace Nomerator
{
    public class CraftDetector : IDisposable
    {
        private readonly string modelFile;
        private readonly MLContext mlContext;

        private readonly PredictionEngine<CraftInput, CraftOutput> predictEngine;
        private InferenceSession _session;
        RunOptions runOptions;

    private bool disposed = false;

        public CraftDetector(string modelFile = "craft-var.onnx", int modelImageWidth = 512, int modelImageHeight = 384)
        {
            this.modelFile = modelFile;

            this.mlContext = new MLContext();

            var dataView = mlContext.Data.LoadFromEnumerable(new List<CraftInput>());
            
            var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    modelFile: this.modelFile,
                    outputColumnNames: new[] {
                               "279", "onnx::Conv_269"},
                    inputColumnNames: new[] {
                               "input.1"});

            var mlNetModel = pipeline.Fit(dataView);

            this.predictEngine = mlContext.Model.CreatePredictionEngine<CraftInput, CraftOutput>(mlNetModel);
            this._session = new InferenceSession("c:/1/refine.onnx");
            runOptions = new RunOptions();

        }

        public DetectionResult Detect(Mat image, float lowText = 0.4f, float textThreshold = 0.6f, float linkThreshold = 0.5f)
        {
            var xx = image.ToInput();
 
            var result = this.predictEngine.Predict(new CraftInput() { Image = xx.Input });
             var inputs = new List<NamedOnnxValue>
             {
                 NamedOnnxValue.CreateFromTensor("y", new DenseTensor<float>(result.Output, new[] { 1, 192, 256, 2 })),
                 NamedOnnxValue.CreateFromTensor("feature", new DenseTensor<float>(result.feature, new[] { 1, 32, 192, 256 }))
             };
            using var input_y = OrtValue.CreateTensorValueFromMemory<float>(OrtMemoryInfo.DefaultInstance,
                result.Output, new long[] { 1, 192, 256, 2 });
            using var input_feature = OrtValue.CreateTensorValueFromMemory<float>(OrtMemoryInfo.DefaultInstance,
                result.feature, new long[] { 1, 32, 192, 256 });
            var inputs2 = new Dictionary<string, OrtValue>
            {
                {
                    "y", input_y
                },
                {
                    "feature", input_feature
                }
            };

            using IDisposableReadOnlyCollection<OrtValue> refinerResults = _session.Run(runOptions, inputs2, _session.OutputNames);
           // var outputShape = new shape(1, 192, 256, 2);
           // var outputArray = np.array(result.Output).reshape(outputShape);
           // var textmap = (ndarray)outputArray["0,:,:,0"];
           // var linkArray = np.array(refinerResults[0].GetTensorDataAsSpan<float>().ToArray());

           // var linkmap = np.reshape(linkArray, new shape(192, 256));//outputArray["0,:,:,1"];

            var outputSize = new Size(256, 192);
            var outputArray2 = result.Output;

            var textmap = new Mat(outputSize, DepthType.Cv32F, 1);         
            var linkmap = new Mat(outputSize, DepthType.Cv32F, 1);
            FillMatFromArray3D(textmap, result.Output);
            linkmap.SetTo<float>(refinerResults[0].GetTensorDataAsSpan<float>().ToArray());
            var img_h = 192;
            var img_w = 256;

            //using Mat textmapMat = textmap.ToMatImage<float>();
            using Mat textScoreThresholded = new Mat();
            using Mat textScoreThresholded2 = new Mat();
            CvInvoke.Threshold(textmap, textScoreThresholded, lowText, 1, ThresholdType.Binary);

            //CvInvoke.Threshold(textmap2, textScoreThresholded2, lowText, 1, ThresholdType.Binary);

            //using Mat linkmapMat = linkmap.ToMatImage<float>();
            using Mat linkScoreThresholded = new Mat();
            using Mat linkScoreThresholded2 = new Mat();
            CvInvoke.Threshold(linkmap, linkScoreThresholded, linkThreshold, 1, ThresholdType.Binary);
            //CvInvoke.Threshold(linkmap2, linkScoreThresholded2, linkThreshold, 1, ThresholdType.Binary);
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
                if (size < 500)
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
                var segmapZero = np.zeros(new shape(192,256 ), dtype: np.UInt8);
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

                var dilate = dilateMat.ToImageNDarray<byte>();

                    for (var i = sy; i < ey; i++)
                    {
                        for (var j = sx; j < ex; j++)
                        {
                            segmap[i, j] = dilate[i - sy, j - sx];
                        }
                    }

                // make box
                var tempArr = np.roll(np.array(np.where(segmap !=0)),  1 , axis: 0);
                var np_contours = tempArr.Transpose(new long[] { 1, 0 }).reshape(-1, 2);
                var rectangle = CvInvoke.MinAreaRect(np_contours.ToPointsArray());
                var boxPoints = CvInvoke.BoxPoints(rectangle);
                var box = boxPoints.FromPointsArray();
               
                allPoints.AddRange(boxPoints);

                // align diamond-shape
                var v0 = Vector<float>.Build.DenseOfArray([(float)box[0, 0], (float)box[0, 1]]);
                var v1 = Vector<float>.Build.DenseOfArray([(float)box[1, 0], (float)box[1, 1]]);
                var v2 = Vector<float>.Build.DenseOfArray([(float)box[2, 0], (float)box[2, 1]]);
                float boxW = (float)v0.Subtract(v1).L2Norm();
                float boxH = (float)v1.Subtract(v2).L2Norm();
                //var boxW = (float)np.linalg.norm(box[0] - box[1]);
                //var boxH = (float)np.linalg.norm(box[1] - box[2]);
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
