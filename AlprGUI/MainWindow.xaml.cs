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
using Serilog;
using Serilog.Sinks.RichTextBox;
using System.Reflection.Emit;
using Serilog.Sinks.RichTextBox.Themes;
using System;
using System.Windows.Forms; 
using System.Drawing;
using MessageBox = System.Windows.MessageBox;

namespace AlprGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _statusMessage;
        private readonly LogBox _logControl;
        private NotifyIcon _notifyIcon;

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
            InitializeTrayIcon();
            StatusMessage = "Application Started";
            Log.Information("Application started");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; 
            this.Hide(); 
            base.OnClosing(e);
        }

        public MainWindow(LogBox logControl): this()
        {
            _logControl = logControl;
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon("icon.ico"),
                Visible = true,
                Text = "LPR"
            };

            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Restore", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;
        }
     
        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
            base.OnClosed(e);
            Environment.Exit(0);
 
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is ListBoxItem selectedItem)
            {
                var textBlock = selectedItem.Content as TextBlock;
                if (textBlock != null)
                {
                    switch (textBlock.Text)
                    {
                        case "Regions":
                            ShowRegionsContent();
                            break;
                        case "Diagnostics":
                            ShowHealthCheckContent();
                            break;
                        case "Virtual COM Ports":
                            ShowComPortsContent();
                            break;
                        case "Cameras":
                            ShowSettingsContent();
                            break;
                        case "LPR Readers":
                            ShowLprReadersContent();
                            break;
                        case "Run service":
                            ShowLprServicesContent();
                            break;
                        case "Log":
                            ShowLogContent();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void ShowLogContent()
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(_logControl);
        }

        private void ShowLprReadersContent()
        {
            var lprControl = new LprReadersControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(lprControl);
        }

        private async void ShowRegionsContent()
        {
            var regionsControl = new RegionsControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(regionsControl);
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            
        }     

        private async Task StartCamera(string cameraName)
        {

        }

        private void ShowSettingsContent()
        {
            var settingsControl = new SettingsControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(settingsControl);
        }

        private void ShowComPortsContent()
        {
            var comPortsControl = new ComPortsControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(comPortsControl);
        }

        private void ShowLprServicesContent()
        {
            var lprServicesControl = new LprServicesControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(lprServicesControl);
        }

        private void ShowHealthCheckContent()
        {
            var healthCheckControl = new HealthCheckControl();
            MainContent.Children.Clear();
            MainContent.Children.Add(healthCheckControl);
        }
    }
}