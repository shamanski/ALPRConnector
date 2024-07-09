using Microsoft.ML.Data;

namespace Nomerator
{
    internal class CraftInput
    {
        [LoadColumn(0)]
        [ColumnName("input.1")]
        [VectorType(1, 320, 96, 3)]
        public float[] Image;
    }
}