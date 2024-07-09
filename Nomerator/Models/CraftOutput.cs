using Microsoft.ML.Data;

namespace Nomerator
{
    internal class CraftOutput
    {
        [ColumnName("285")]
        public float[] Output;

        [ColumnName("onnx::Conv_275")]
        public float[] feature;
    }
}