using AppDomain;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AlprGUI
{
    public partial class LprReaderForm : UserControl
    {
        public event EventHandler<LprReaderEventArgs> SaveClicked;
        public event EventHandler CancelClicked;
        public LprReader Reader { get; set; }
        private readonly LprReaderManager readerManager;
        private readonly ComPortsManager portsManager;
        private readonly CameraManager cameraManager;

        public ObservableCollection<ComPortPair> ComPortPairs { get; set; }
        public ObservableCollection<Camera> Cameras { get; set; }

        public LprReaderForm()
        {
            InitializeComponent();
            readerManager = new LprReaderManager();
            portsManager = new ComPortsManager(new ModemEmulatorService());
            cameraManager = new CameraManager();
            ComPortPairs = new ObservableCollection<ComPortPair>(portsManager.GetAll());
            Cameras = new ObservableCollection<Camera>(cameraManager.GetAll());

            this.DataContext = this;

            ComPortPairComboBox.ItemsSource = ComPortPairs;
            if (ComPortPairs.Count > 0)
            {
                ComPortPairComboBox.SelectedIndex = 0;
            }

            CameraComboBox.ItemsSource = Cameras;
            if (Cameras.Count > 0)
            {
                CameraComboBox.SelectedIndex = 0;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RS485AddrTextBox.Text, out int rs485Addr))
            {
                MessageBox.Show("Invalid RS485 Address", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var reader = new LprReader
            {
                Name = NameTextBox.Text,
                RS485Addr = rs485Addr,
                ComPortPair = ComPortPairComboBox.SelectedItem as ComPortPair,
                Camera = CameraComboBox.SelectedItem as Camera
            };

            var context = new ValidationContext(reader, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(reader, context, results, true);

            if (isValid)
            {
                SaveClicked?.Invoke(this, new LprReaderEventArgs(reader));
            }
            else
            {
                string errors = string.Join("\n", results.Select(r => r.ErrorMessage));
                MessageBox.Show(errors, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    public class LprReaderEventArgs : EventArgs
    {
        public LprReader Reader { get; }

        public LprReaderEventArgs(LprReader reader)
        {
            Reader = reader;
        }
    }
}
