using AppDomain;
using System;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AlprGUI
{
    public partial class CameraForm : UserControl
    {
        public event EventHandler<CameraEventArgs> SaveClicked;
        public event EventHandler CancelClicked;
        public Camera Camera { get; set; }

        public CameraForm()
        {
            InitializeComponent();
            this.DataContext = Camera;
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
