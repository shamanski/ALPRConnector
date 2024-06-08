using AppDomain;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows;

namespace AlprGUI;

public partial class LprServicesControl : UserControl
{
    private readonly LprReaderRepository readerManager;
    private LprReadersViewModel _viewModel;
    private readonly PortAdapterManager _portAdapterManager = PortAdapterManager.Instance;

    public ObservableCollection<LprReaderViewModel> Readers { get; } = new ObservableCollection<LprReaderViewModel>();

    public LprServicesControl()
    {
        InitializeComponent();
        readerManager = new LprReaderRepository();
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
            var readerViewModel = new LprReaderViewModel(reader);
            readerViewModel.Status = PortAdapterManager.Instance.GetAdapterStatus(reader);
            Readers.Add(readerViewModel);

            PortAdapterManager.Instance.AdapterStatusChanged += (sender, args) =>
            {
                if (args.Reader == reader)
                {
                    readerViewModel.Status = args.Status;
                }
            };
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (dataGrid.SelectedItem is LprReaderViewModel selectedReader)
        {
           // selectedReader.Status = "Starting...";

            try
            {
                await _portAdapterManager.StartAdapterAsync(selectedReader.LprReader);
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
            _portAdapterManager.StopAdapter(selectedReader.LprReader);
           // selectedReader.Status = "Stopped";
        }
    }
}
