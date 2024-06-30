﻿
using System;
using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML;
using Numpy;

namespace Nomerator
{
    public class CraftDetector : IDisposable
    {
        private readonly string modelFile;

        private readonly int modelImageWidth;

        private readonly int modelImageHeight;

        private readonly MLContext mlContext;

        private readonly PredictionEngine<CraftInput, CraftOutput> predictEngine;

        private bool disposed = false;

        public CraftDetector(string modelFile = "craft-var.onnx", int modelImageWidth = 512, int modelImageHeight = 384)
        {
            this.modelFile = modelFile;
            this.modelImageWidth = modelImageWidth;
            this.modelImageHeight = modelImageHeight;

            this.mlContext = new MLContext();

            var dataView = mlContext.Data.LoadFromEnumerable(new List<CraftInput>());
            var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    modelFile: this.modelFile,
                    outputColumnNames: new[] {
                               "279"},
                    inputColumnNames: new[] {
                               "input.1"});

            var mlNetModel = pipeline.Fit(dataView);

            this.predictEngine = mlContext.Model.CreatePredictionEngine<CraftInput, CraftOutput>(mlNetModel);
        }

        public DetectionResult Detect(Mat image, float lowText = 0.4f, float textThreshold = 0.5f, float linkThreshold = 0.6f)
        {
            int modelInputImageWidth = 512;
            int modelImageHeight = 384;           
            using var inputData = image.ToImageNDarray<byte>();

            var resizedDataTuple = inputData.ResizeAspectRatio(modelInputImageWidth, modelImageHeight, Inter.Linear);

            var ratio_w = 1.0f  / resizedDataTuple.Ratio;
            var ratio_h = 1.0f / resizedDataTuple.Ratio;

            using var resizedImage = resizedDataTuple.Image;

            // preprocessing
            using var resizedImageNormalized = resizedImage.NormalizeMeanVariance(
                mean: new NDarray(new float[] { 0.485f, 0.456f, 0.406f }),
                variance: new NDarray(new float[] { 0.229f, 0.224f, 0.225f }));

            // [h, w, c] to [c, h, w]
            using var finaleImage = resizedImageNormalized.transpose(2, 0, 1);
            var xx = finaleImage.astype(np.float32).GetData<float>()[500000..500010];
            var result = this.predictEngine.Predict(new CraftInput() { Image = finaleImage.astype(np.float32).GetData<float>() });

            using var outputArray = np.reshape(result.Output, 1, 192, 256, 2);
            using var textmap = outputArray["0,:,:,0"];
            using var linkmap = outputArray["0,:,:,1"];

            var img_h = textmap.shape[0];
            var img_w = textmap.shape[1];

            using Mat textmapMat = textmap.ToMatImage<float>();
            using Mat textScoreThresholded = new Mat();
            CvInvoke.Threshold(textmapMat, textScoreThresholded, lowText, 1, ThresholdType.Binary);

            using Mat linkmapMat = linkmap.ToMatImage<float>();
            using Mat linkScoreThresholded = new Mat();
            CvInvoke.Threshold(linkmapMat, linkScoreThresholded, linkThreshold, 1, ThresholdType.Binary);

            var scoreText = textScoreThresholded.ToImageNDarray<float>();
            var scoreLink = linkScoreThresholded.ToImageNDarray<float>();

            using var text_score_comb = np.clip(scoreText + scoreLink, (NDarray)0, (NDarray)1);

            using var text_score_comb_bytes = text_score_comb.astype(np.uint8);

            using var labelsMat = new Mat();
            using var statsMat = new Mat();
            using var centroidsMat = new Mat();
            using var text_score_comb_data = text_score_comb_bytes.ToMatImage<byte>();
            var nLabels = CvInvoke.ConnectedComponentsWithStats(text_score_comb_data, labelsMat, statsMat, centroidsMat, connectivity: LineType.FourConnected);

            using var stats = statsMat.ToImageNDarray<int>();
            using var labels = labelsMat.ToImageNDarray<int>();
            using var centroids = centroidsMat.ToImageNDarray<float>();
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

                using var labelFlags = labels.equals(k);
                using var textMapArr = textmap.WhereFlags<float>(labelFlags, (flag, elem) => flag ? elem : 0.0f);

      
                if ((float)np.max(textMapArr) < textThreshold)
                {
                   continue;
                }

                // make segmentation map
                using var segmapZero = np.zeros(textmap.shape, dtype: np.uint8);
                using var segmap1 = segmapZero.WhereFlags<byte>(labelFlags, (flag, elem) => (byte)(flag ? 255 : 0));
                using var segmap = segmap1.WhereFlags<byte>(np.logical_and(scoreLink.equals(1), scoreText.equals(0)), (flag, elem) => (byte)(flag ? 0 : elem));

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
                using var segmapMat = segmap[$"{sy}:{ey},{sx}:{ex}"].ToMatImage<byte>();
                CvInvoke.Dilate(segmapMat, dilateMat, kernelMat, new Point(-1, -1), -1, BorderType.Default, new MCvScalar());

                using var dilate = dilateMat.ToImageNDarray<byte>();
                for (var i = sy; i < ey; i++)
                {
                    for (var j = sx; j < ex; j++)
                    {
                        segmap[i, j] = dilate[i - sy, j - sx];
                    }
                }

                // make box
                using var tempArr = np.roll(np.array(np.where(segmap.not_equals(0))), new int[] { 1 }, axis: 0);
                using var np_contours = tempArr.transpose(1, 0).reshape(-1, 2);
                var rectangle = CvInvoke.MinAreaRect(np_contours.ToPointsArray());
                var boxPoints = CvInvoke.BoxPoints(rectangle);
                var box = boxPoints.FromPointsArray();
               
                allPoints.AddRange(boxPoints);

                // align diamond-shape
                var boxW = (float)np.linalg.norm(box[0] - box[1]);
                var boxH = (float)np.linalg.norm(box[1] - box[2]);
                var box_ratio = Math.Max(boxW, boxH) / (Math.Min(boxW, boxH) + 1e-5);
                if (Math.Abs(1 - box_ratio) <= 0.2)
                {
                    var l = (float)np.min(np_contours[":,0"]);
                    var r = (float)np.max(np_contours[":,0"]);
                    var t = (float)np.min(np_contours[":,1"]);
                    var b = (float)np.max(np_contours[":,1"]);
                    box = np.array(new float[,] { { l, t }, { r, t }, { r, b }, { l, b } }, dtype: np.float32);
                }

                // make clock-wise order
                var startidx = (int)box.sum(axis: 1).argmin();
                box = np.roll(box, new int[] { 4 - startidx }, axis: 0);
                box = np.array(box);
                boxes.Add(k, box.ToPointsArray().AdjustResultCoordinates(1, 1));
                
                box.Dispose();
            }

            return new DetectionResult
            {
                Boxes = boxes,
                ScoreText = scoreText,
                ScoreLink = scoreLink,
                OutputImage =  resizedDataTuple.OutputImageMat
        };
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
