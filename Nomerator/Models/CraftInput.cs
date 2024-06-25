using Microsoft.ML.Data;

namespace Nomerator
{
    internal class CraftInput
    {
        [LoadColumn(0)]
        [ColumnName("input.1")]
        [VectorType(1, 512, 384, 3)]
        public float[] Image;
    }
}