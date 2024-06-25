using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Emgu.CV;
using System.Windows;
using System.Threading.Tasks;
using AppDomain;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;

namespace AlprGUI
{
    public partial class RegionsControl : UserControl
    {
        private readonly VideoCaptureService videoCaptureManager;
        private readonly CameraRepository cameraManager;
        private Point startPoint;
        private Rectangle selectionRectangle;
        private bool isSelectingArea = false;

        public RegionsControl()
        {
            InitializeComponent();
 
            cameraManager = new CameraRepository();
            cameraComboBox.ItemsSource = cameraManager.GetAll();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraComboBox.SelectedItem is Camera selectedCamera)
            {
                var connection = cameraManager.GetConnectionString(selectedCamera);
                await StartCamera(connection);
            }
        }

        private async Task StartCamera(string connection)
        {
            try
            {
                var videoCaptureManager = VideoCaptureService.Instance;
                await videoCaptureManager.StartProcessingAsync(connection, async frame =>
                {
                    // Resize the frame to a smaller size
                   // var resizedFrame = videoCaptureManager.ResizeFrame(frame, new System.Drawing.Size(704, 576));

                    await Dispatcher.InvokeAsync(() =>
                    {
                        imageControl.Source = ToBitmapSource(frame);
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void SelectAreaButton_Click(object sender, RoutedEventArgs e)
        {
            isSelectingArea = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectionRectangle != null)
            {
                double left = Canvas.GetLeft(selectionRectangle);
                double top = Canvas.GetTop(selectionRectangle);
                double width = selectionRectangle.Width;
                double height = selectionRectangle.Height;

                // Сохранение выделенной области
                // Здесь вы можете реализовать свою логику для сохранения координат или изображения выделенной области

                MessageBox.Show($"Область сохранена: Left={left}, Top={top}, Width={width}, Height={height}");
            }
        }

        private void ImageControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isSelectingArea)
            {
                Point position = e.GetPosition(canvas); // Получаем координаты относительно imageControl
                if (position.X >= 0 && position.X <= canvas.ActualWidth &&
                    position.Y >= 0 && position.Y <= canvas.ActualHeight)
                {
                    startPoint = position;
                    selectionRectangle = new Rectangle
                    {
                        Stroke = Brushes.Red,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(selectionRectangle, startPoint.X);
                    Canvas.SetTop(selectionRectangle, startPoint.Y);
                    Canvas.SetZIndex(selectionRectangle, 1000);
                    canvas.Children.Add(selectionRectangle);
                }
            }
        }

        private void ImageControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelectingArea && selectionRectangle != null)
            {
                Point position = e.GetPosition(canvas); // Получаем координаты относительно imageControl
                double left = Math.Min(startPoint.X, position.X);
                double top = Math.Min(startPoint.Y, position.Y);
                double width = Math.Abs(startPoint.X - position.X);
                double height = Math.Abs(startPoint.Y - position.Y);

                selectionRectangle.Margin = new Thickness(left, top, 0, 0);
                selectionRectangle.Width = width;
                selectionRectangle.Height = height;
            }
        }

        private void ImageControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelectingArea)
            {
                isSelectingArea = false;

                // Convert coordinates from canvas to imageControl
                Point startPointImage = canvas.TranslatePoint(startPoint, canvas);
                Point endPointImage = canvas.TranslatePoint(e.GetPosition(canvas), canvas);

                // Use startPointImage and endPointImage as coordinates relative to imageControl
                // for further processing
            }
        }

        private BitmapSource ToBitmapSource(Mat mat)
        {
            using (var bitmap = mat.ToBitmap())
            {
                IntPtr hBitmap = bitmap.GetHbitmap();
                BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze(); 
                return bitmapSource;
            }
        }
    }
}
