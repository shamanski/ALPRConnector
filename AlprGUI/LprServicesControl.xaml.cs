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
        private LprReadersViewModel _viewModel;
        private readonly LprReaderRepository readerManager;
        public ObservableCollection<LprReaderViewModel> Readers { get; set; }
        private Dictionary<object, CancellationTokenSource> _cancellationTokenSources = new Dictionary<object, CancellationTokenSource>();
        private readonly Dictionary<LprReaderViewModel, PortAdapter> _portAdapters = new Dictionary<LprReaderViewModel, PortAdapter>();

        public LprServicesControl()
        {
            InitializeComponent();
            readerManager = new LprReaderRepository();
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
                    var comPortService = new ComPortService();
                    var portAdapter = new PortAdapter(comPortService, selectedReader.LprReader);
                    _portAdapters[selectedReader] = portAdapter;

                    await portAdapter.Run();
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
            if (dataGrid.SelectedItem is LprReaderViewModel selectedReader &&
                _cancellationTokenSources.TryGetValue(selectedReader, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                _cancellationTokenSources.Remove(selectedReader);

                if (_portAdapters.TryGetValue(selectedReader, out var portAdapter))
                {
                    portAdapter.Dispose();
                    _portAdapters.Remove(selectedReader);
                }

                selectedReader.Status = "Stopped";
            }
        }
    }
}
