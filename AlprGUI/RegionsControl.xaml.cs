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
        private readonly VideoCaptureManager videoCaptureManager;
        private readonly CameraManager cameraManager;
        private Point startPoint;
        private Rectangle selectionRectangle;
        private bool isSelectingArea = false;

        public RegionsControl()
        {
            InitializeComponent();
 
            cameraManager = new CameraManager();
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
                var videoCaptureManager = VideoCaptureManager.Instance;
                await videoCaptureManager.StartProcessingAsync(connection, frame =>
                {
                    Dispatcher.Invoke(() =>
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

        private void SelectAreaButton_Click(object sender, RoutedEventArgs e)
        {
            // Логика для выбора области LPR
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isSelectingArea)
            {
                Point position = e.GetPosition(canvas);
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
            else
            {
                Point endPoint = e.GetPosition(canvas);
                double left = Math.Min(startPoint.X, endPoint.X);
                double top = Math.Min(startPoint.Y, endPoint.Y);
                double width = Math.Abs(startPoint.X - endPoint.X);
                double height = Math.Abs(startPoint.Y - endPoint.Y);

                selectionRectangle.Margin = new Thickness(left, top, 0, 0);
                selectionRectangle.Width = width;
                selectionRectangle.Height = height;

                isSelectingArea = false;

            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Логика для обработки движения мыши на canvas
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
                bitmapSource.Freeze(); // Размораживаем изображение, чтобы его можно было использовать в разных потоках
                return bitmapSource;
            }
        }
    }
}
