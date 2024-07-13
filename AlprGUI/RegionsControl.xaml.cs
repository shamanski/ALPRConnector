using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Emgu.CV;
using System.Windows;
using AppDomain;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using Camera = AppDomain.Camera;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;

namespace AlprGUI
{
    public partial class RegionsControl : UserControl
    {
        private readonly CameraRepository cameraManager;
        private Rectangle selectionRectangle;
        private bool isSelectingArea = false;
        private double _ratio;
        private Point? firstCorner;
        private Point? secondCorner;
        private Camera currentCamera;

        public RegionsControl()
        {
            InitializeComponent();
 
            cameraManager = new CameraRepository();
            cameraComboBox.ItemsSource = cameraManager.GetAll();
            selectionRectangle = new Rectangle() 
            {   Width = canvas.ActualWidth, 
                Height = canvas.ActualHeight,
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
            };
            Canvas.SetLeft(selectionRectangle, 0);
            Canvas.SetTop(selectionRectangle, 0);
            Canvas.SetZIndex(selectionRectangle, 1000);
            canvas.Children.Add( selectionRectangle );
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraComboBox.SelectedItem is Camera selectedCamera)
            {
                await StartCamera(selectedCamera);
            }
        }

        private async Task StartCamera(Camera camera)
        {
            try
            {
                var videoCaptureManager = VideoCaptureService.Instance;
                var connection = cameraManager.GetConnectionString(camera);
                currentCamera = camera;
                await videoCaptureManager.StartProcessingAsync(connection, async frame =>
                {
                    int canvasWidth = (int)canvas.ActualWidth;
                    int canvasHeight = (int)canvas.ActualHeight;
                   
                    using var resizedFrame = videoCaptureManager.ResizeFrame(frame, new System.Drawing.Size(canvasWidth, canvasHeight), out _ratio );

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!isSelectingArea)
                        {
                            Canvas.SetLeft(selectionRectangle, camera.Roi.RelativeRoiLeft * resizedFrame.Width);
                            Canvas.SetTop(selectionRectangle, camera.Roi.RelativeRoiTop * resizedFrame.Height);
                            selectionRectangle.Width = camera.Roi.RelativeRoiWidth * resizedFrame.Width;
                            selectionRectangle.Height = camera.Roi.RelativeRoiHeight * resizedFrame.Height;
                        }
                        
                        imageControl.Source = ToBitmapSource(resizedFrame);
                    });
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private async void SelectAreaButton_Click(object sender, RoutedEventArgs e)
        {
            isSelectingArea = true;
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            currentCamera.Roi = new RelativeRectangle() 
            {
                RelativeRoiTop = 0,
                RelativeRoiLeft = 0,
                RelativeRoiHeight = 1,
                RelativeRoiWidth = 1
            };
            cameraManager.EditCamera(currentCamera);
            MessageBox.Show("ROI saved.");
        }

        private void ImageControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isSelectingArea)
            {
                Point position = e.GetPosition(canvas);
                if (firstCorner == null && position.X >= 0 && position.X <= canvas.ActualWidth &&
                    position.Y >= 0 && position.Y <= canvas.ActualHeight)
                {
                    firstCorner = position;
                    Canvas.SetLeft(selectionRectangle, position.X);
                    Canvas.SetTop(selectionRectangle, position.Y);
                    selectionRectangle.Width = 0;
                    selectionRectangle.Height = 0;
                }
                else if (position.X >= 0 && position.X <= canvas.ActualWidth &&
                        position.Y >= 0 && position.Y <= canvas.ActualHeight )
                {
                    secondCorner = position;
                    selectionRectangle.Width = Math.Abs(firstCorner.Value.X - position.X);
                    selectionRectangle.Height = Math.Abs(firstCorner.Value.Y - position.Y);
                    
                    isSelectingArea = false;
                    currentCamera.Roi = new RelativeRectangle()
                    {
                        RelativeRoiTop = firstCorner.Value.Y / imageControl.ActualHeight,
                        RelativeRoiLeft = firstCorner.Value.X / imageControl.ActualWidth,
                        RelativeRoiHeight = selectionRectangle.Height / imageControl.ActualHeight,
                        RelativeRoiWidth = selectionRectangle.Width / imageControl.ActualWidth
                    }; 
                    cameraManager.EditCamera(currentCamera);
                    secondCorner = null;
                    firstCorner = null;
                    MessageBox.Show("ROI saved.");
                }
            }
        }

        private void ImageControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isSelectingArea && selectionRectangle != null && firstCorner != null)
            {
                Point position = e.GetPosition(canvas); // Получаем координаты относительно imageControl
                double width = Math.Abs(firstCorner.Value.X - position.X);
                double height = Math.Abs(firstCorner.Value.Y - position.Y);

                selectionRectangle.Width = width;
                selectionRectangle.Height = height;
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
                mat.Dispose();
                return bitmapSource;
            }
        }
    }
}
