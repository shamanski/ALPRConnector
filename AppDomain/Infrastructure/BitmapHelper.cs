using System.Drawing;
using System.Windows.Media;

using System.Drawing;
using Pen = System.Drawing.Pen;
namespace AppDomain;
public class BitmapHelper
{
    public static Bitmap ConvertToNonIndexedImage(Bitmap original)
    {
        Bitmap newBitmap = new Bitmap(original.Width, original.Height);
        using (Graphics graphics = Graphics.FromImage(newBitmap))
        {
            graphics.DrawImage(original, 0, 0);
        }
        return newBitmap;
    }

    public static void DrawRectangleOnBitmap(Bitmap bitmap, Rectangle rectangle, System.Drawing.Color color, int thickness)
    {
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            using (Pen pen = new Pen(color, thickness))
            {
                graphics.DrawRectangle(pen, rectangle);
            }
        }
    }
}
