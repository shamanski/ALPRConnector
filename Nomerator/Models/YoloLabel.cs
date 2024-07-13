namespace Nomerator
{
    public class YoloLabel
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public YoloLabelKind Kind { get; set; }
    }

    public enum YoloLabelKind
    {
        Generic,
        IstanceSeg,
    }
}
