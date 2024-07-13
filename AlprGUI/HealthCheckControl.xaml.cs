using AppDomain;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;


namespace AlprGUI
{
    public partial class HealthCheckControl : UserControl
    {
            private readonly ObservableCollection<KeyValuePair<string, string>> _healthCheckResults;
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            public HealthCheckControl()
            {
                InitializeComponent();

                _healthCheckResults = new ObservableCollection<KeyValuePair<string, string>>();
                HealthCheckListView.ItemsSource = _healthCheckResults;

                StartHealthCheckLoop();
            }

            private async void StartHealthCheckLoop()
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var results = await HealthCheck.CheckAllServicesAsync();

                    Dispatcher.Invoke(() =>
                    {
                        _healthCheckResults.Clear();
                        foreach (var result in results)
                        {
                            _healthCheckResults.Add(result);
                        }
                    });

                    await Task.Delay(5000);
                }
            }

            protected override void OnInitialized(EventArgs e)
            {
                base.OnInitialized(e);
                this.Unloaded += OnUnloaded;
            }

            private async void OnUnloaded(object sender, EventArgs e)
            {
                _cancellationTokenSource.Cancel();
            }
        }
}
