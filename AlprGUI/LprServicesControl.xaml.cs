using AppDomain;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Emgu.CV;
using openalprnet;
using System.Threading;

namespace AlprGUI
{
    public partial class LprServicesControl : UserControl
    {
        private readonly CameraManager cameraManager;
        private LprReadersViewModel _viewModel;
        private readonly LprReaderManager readerManager;
        private readonly OpenAlprService alprClient;
        public ObservableCollection<LprReaderViewModel> Readers { get; set; }
        private Dictionary<object, CancellationTokenSource> _cancellationTokenSources = new Dictionary<object, CancellationTokenSource>();

        public LprServicesControl()
        {
            InitializeComponent();
            cameraManager = new CameraManager();
            readerManager = new LprReaderManager();
            alprClient = new OpenAlprService();
            Readers = new ObservableCollection<LprReaderViewModel>();
            LoadReaders();
            _viewModel = new LprReadersViewModel();
            _viewModel.LprReaders = Readers;
            this.DataContext = _viewModel;
        }

        private void LoadReaders()
        {
            var readers = readerManager.GetAll();
            foreach (var reader in readers)
            {
                Readers.Add(new LprReaderViewModel(reader));
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is LprReaderViewModel selectedReader)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokenSources[selectedReader] = cancellationTokenSource;

                selectedReader.Status = "Starting...";

                try
                {
                    var connection = cameraManager.GetConnectionString(selectedReader.LprReader.Camera);
                    var comPortService = new ComPortService(selectedReader.LprReader.ComPortPair.Sender, selectedReader.LprReader.RS485Addr);

                    var videoCapture = await alprClient.CreateVideoCaptureAsync(connection, cancellationTokenSource.Token);

                    await alprClient.StartProcessingAsync(videoCapture, result =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            selectedReader.Status = $"Running - Last plate: {result}";
                            comPortService.SendLpAsync(result);
                        });
                    }, cancellationTokenSource.Token);

                    selectedReader.Status = "Running";
                }
                catch (Exception ex)
                {
                    selectedReader.Status = $"Error: {ex.Message}";
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is LprReaderViewModel selectedReader)
            {
                if (_cancellationTokenSources.TryGetValue(selectedReader, out var cancellationTokenSource))
                {
                    cancellationTokenSource.Cancel();
                    selectedReader.Status = "Stopped";
                }
            }
        }
    }
}
