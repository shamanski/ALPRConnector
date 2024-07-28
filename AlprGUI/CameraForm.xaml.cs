using AppDomain;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AlprGUI
{
    public partial class CameraForm : System.Windows.Controls.UserControl
    {
        public event EventHandler<CameraEventArgs> SaveClicked;
        public event EventHandler CancelClicked;
        public Camera Camera { get; set; } = new Camera();
        public ObservableCollection<string> Manufacturers { get; set; } = new();

        public CameraForm()
        {
            InitializeComponent();
            this.DataContext = Camera;
            ManufacturerComboBox.ItemsSource = Manufacturers;

            LoadManufacturersFromSettings();
            ManufacturerComboBox.SelectedIndex = 0;
        }

        public CameraForm(Camera camera)
        {
            InitializeComponent();
            this.DataContext = Camera;
            this.Camera = camera;
            ManufacturerComboBox.ItemsSource = Manufacturers;

            LoadManufacturersFromSettings();
            ManufacturerComboBox.SelectedIndex = Manufacturers.IndexOf(Camera.Manufacturer);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var camera = new Camera
            {
                Name = CameraNameTextBox.Text,
                IpAddress = CameraAddressTextBox.Text,
                IpPort = CameraPortTextBox.Text,
                Login = CameraLoginTextBox.Text,
                Password = CameraPasswordTextBox.Text,
                Manufacturer = ManufacturerComboBox.Text
            };

            var context = new ValidationContext(camera, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(camera, context, results, true);

            if (isValid)
            {
                // If the model is valid, invoke the SaveClicked event
                SaveClicked?.Invoke(this, new CameraEventArgs(camera));
            }
            else
            {
                // If the model is invalid, display validation errors
                string errors = string.Join("\n", results.Select(r => r.ErrorMessage));
                MessageBox.Show(errors, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LoadManufacturersFromSettings()
        {
            Manufacturers.Clear();
            var appSettings = ConfigurationLoader.LoadSettings();
            if (appSettings?.Cameras != null)
            {
                foreach (var m in appSettings.ConnectionTemplates.Keys)
                {
                    Manufacturers.Add(m);
                }
            }
        }
    }

    public class CameraEventArgs : EventArgs
    {
        public Camera Camera { get; }

        public CameraEventArgs(Camera camera)
        {
            Camera = camera;
        }
    }

}
