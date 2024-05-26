using AppDomain;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Camera = AppDomain.Camera;

namespace AlprGUI
{
    public partial class SettingsControl : UserControl
    {
        private readonly CameraManager cameraManager;

        public ObservableCollection<Camera> Cameras { get; set; }
        private AppSettings appSettings { get; set; }
        
        public SettingsControl()
        {
            InitializeComponent();
            cameraManager = new CameraManager();
            Cameras = new ObservableCollection<Camera>();
            CamerasList.ItemsSource = Cameras;
            appSettings = ConfigurationLoader.LoadSettings();
            ChangeVisibility();
            LoadCamerasFromSettings();
        }

        private void AddCameraButton_Click(object sender, RoutedEventArgs e)
        {
            CameraForm cameraForm = new CameraForm();
            cameraForm.SaveClicked += SaveCameraButton_Click;
            cameraForm.CancelClicked += CameraForm_CancelClicked;
            HideCameraFields();
            CameraFieldsStackPanel.Children.Add(cameraForm);
        }

        private void EditCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (CamerasList.SelectedItem is Camera selectedCamera)
            {
                CameraForm cameraForm = new CameraForm();
                cameraForm.SaveClicked += SaveCameraButton_Click;
                cameraForm.CancelClicked += CameraForm_CancelClicked;
                var camera = cameraManager.GetCameraByName(selectedCamera.Name);
                cameraForm.Camera = camera;
                cameraForm.DataContext = camera;
                CameraFieldsStackPanel.Children.Add(cameraForm);
                HideCameraFields();              
            }
        }

        private void SaveCameraButton_Click(object sender, CameraEventArgs e)
        {
            if (sender is CameraForm cameraForm)
            {
                cameraForm.SaveClicked -= SaveCameraButton_Click;
                cameraForm.CancelClicked -= CameraForm_CancelClicked;
                ChangeVisibility();
                cameraForm.SaveClicked -= SaveCameraButton_Click;
                cameraForm.CancelClicked -= CameraForm_CancelClicked;
                CameraFieldsStackPanel.Children.Remove(cameraForm);
                try
                {
                    cameraManager.AddCamera(e.Camera);
                }
                catch (Exception ex)
                {
                    MessageBoxResult result = MessageBox.Show(ex.Message);
                }
                LoadCamerasFromSettings();
            }
        }

        public void HideCameraFields()
        {
            AddCameraButton.IsEnabled = false;
            EditCameraButton.IsEnabled = false;
            RemoveCameraButton.IsEnabled = false;
            CamerasList.IsEnabled = false;
        }

        private void RemoveCameraButton_Click(object sender, RoutedEventArgs e)
        {
            Camera selectedCamera = CamerasList.SelectedItem as Camera;

            if (selectedCamera != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure?",
                                                   "Remove confirmation",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    cameraManager.RemoveCamera(selectedCamera.Name);
                    Cameras.Remove(selectedCamera);
                }
            }
        }

        private void CameraForm_CancelClicked(object sender, EventArgs e)
        {
            if (sender is CameraForm cameraForm)
            {
                cameraForm.SaveClicked -= SaveCameraButton_Click;
                cameraForm.CancelClicked -= CameraForm_CancelClicked;
                CameraFieldsStackPanel.Children.Remove(cameraForm);
                ChangeVisibility();
            }
        }

        private void CamerasList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeVisibility();
        }

        private void ChangeVisibility()
        {
            bool isSelected = CamerasList.SelectedItem != null;
            EditCameraButton.IsEnabled = isSelected;
            RemoveCameraButton.IsEnabled = isSelected;
            AddCameraButton.IsEnabled = true;
            CamerasList.IsEnabled = true;
        }

        private void LoadCamerasFromSettings()
        {
            Cameras.Clear();
            appSettings = ConfigurationLoader.LoadSettings();
            if (appSettings?.Cameras != null)
            {
                foreach (var camera in appSettings.Cameras)
                {
                    Cameras.Add(camera);
                }
            }
        }
    }
}
