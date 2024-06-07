using AppDomain;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class LprReaderViewModel : INotifyPropertyChanged
{
    public LprReader LprReader { get; set; }

    private string _status;
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public LprReaderViewModel(LprReader lprReader)
    {
        LprReader = lprReader;
        Status = "Stopped";
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LprReadersViewModel : INotifyPropertyChanged
{
    private ObservableCollection<LprReaderViewModel> _lprReaders;

    public ObservableCollection<LprReaderViewModel> LprReaders
    {
        get => _lprReaders;
        set
        {
            _lprReaders = value;
            OnPropertyChanged();
        }
    }

    public LprReadersViewModel()
    {
        _lprReaders = new ObservableCollection<LprReaderViewModel>();
    }

    public async Task StartProcessingAsync(LprReaderViewModel selectedReader)
    {
        selectedReader.Status = "Starting...";
        OnPropertyChanged(nameof(LprReaders));

        try
        {
            await Task.Delay(1000); 


            await Task.Run(() =>
            {
                Task.Delay(5000).Wait();
            });

            selectedReader.Status = "Running";
        }
        catch (Exception ex)
        {
            selectedReader.Status = $"Error: {ex.Message}";
        }

        OnPropertyChanged(nameof(LprReaders));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
