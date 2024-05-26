using AppDomain;
using Emgu.CV;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AlprGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _statusMessage;
        private bool isSelectingArea = false;
        private Point startPoint;
        private Rectangle selectionRectangle;
        private Canvas canvas;
        private VideoCaptureManager videoCaptureManager;
        private Image imageControl;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public MainWindow()
        {
            InitializeComponent();            
            DataContext = this;
            StatusMessage = "Application Started";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ListBoxItem selectedItem)
            {
                var textBlock = selectedItem.Content as TextBlock;
                if (textBlock != null)
                {
                    switch (textBlock.Text)
                    {
                        case "Home":
                            ShowHomeContent();
                            break;
                        case "Settings":
                            //ShowSettingsContent();
                            break;
                        case "About":
                            //ShowAboutContent();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private async void ShowHomeContent()
        {
            MainContent.Children.Clear();

            var grid = new Grid();

            var rowDefinition1 = new RowDefinition();
            var rowDefinition2 = new RowDefinition();
            rowDefinition1.Height = new GridLength(1, GridUnitType.Star); 
            rowDefinition2.Height = GridLength.Auto; 
            grid.RowDefinitions.Add(rowDefinition1);
            grid.RowDefinitions.Add(rowDefinition2);
            canvas = new Canvas();
            imageControl = new Image();
            imageControl.Stretch = Stretch.Uniform;
            await StartCamera("default");
            canvas.Children.Add(imageControl);
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            grid.Children.Add(canvas);
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var selectAreaButton = new Button
            {
                Content = "Выделить область",
                Margin = new Thickness(5)
            };
            selectAreaButton.Click += SelectAreaButton_Click;
            buttonPanel.Children.Add(selectAreaButton);

            var otherButton1 = new Button
            {
                Content = "Button 1",
                Margin = new Thickness(5)
            };
            buttonPanel.Children.Add(otherButton1);

            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            MainContent.Children.Add(grid);
        }

        private void SelectAreaButton_Click(object sender, RoutedEventArgs e)
        {
            isSelectingArea = true;
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
            if (isSelectingArea && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(canvas); 
                double left = Math.Min(startPoint.X, currentPoint.X);
                double top = Math.Min(startPoint.Y, currentPoint.Y);
                double width = Math.Abs(startPoint.X - currentPoint.X);
                double height = Math.Abs(startPoint.Y - currentPoint.Y);

                Canvas.SetLeft(selectionRectangle, left);
                Canvas.SetTop(selectionRectangle, top);
                selectionRectangle.Width = width;
                selectionRectangle.Height = height;
            }
        }

        private async Task StartCamera(string cameraName)
        {
            var videoCaptureManager = VideoCaptureManager.Instance;
            await videoCaptureManager.StartProcessingAsync(cameraName, frame =>
            {
                // Обработчик кадров
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Обновление интерфейса
                    BitmapSource bitmapSource = ToBitmapSource(frame);
                    imageControl.Source = bitmapSource;
                });
            });
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