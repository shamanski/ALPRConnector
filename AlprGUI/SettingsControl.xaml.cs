using AppDomain;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace AlprGUI
{
    public partial class SettingsControl : UserControl
    {
        public ObservableCollection<Camera> Cameras { get; set; }
        private bool isEditing;
        TextBox cameraNameTextBox;
        TextBox cameraAddressTextBox;
        Button saveButton;
        Button cancelButton;

        public SettingsControl()
        {
            InitializeComponent();
            Cameras = new ObservableCollection<Camera>();
            CameraList.ItemsSource = Cameras;
        }

        private void AddCameraButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCameraFields();
        }

        private void EditCameraButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCameraFields();
            // Здесь также можно заполнить текстовые поля текущими значениями камеры для редактирования
        }

        private void SaveCameraButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохранение или обновление информации о камере
            if (isEditing)
            {
                Camera newCamera = new Camera();
                newCamera.Name = cameraNameTextBox.Text;
                newCamera.IpAddress = cameraAddressTextBox.Text;
                //newCamera.Description = cameraDescriptionTextBox.Text;
                Cameras.Add(newCamera);
            }
            else
            {
                
            }

            // Скрытие текстовых полей и кнопок после сохранения
            HideCameraFields();
            isEditing = false;
        }

        private void CancelCameraButton_Click(object sender, RoutedEventArgs e)
        {
            // Скрытие текстовых полей и кнопок при отмене
            HideCameraFields();
        }

        public void ShowCameraFields()
        {
            isEditing = true;
            AddCameraButton.IsEnabled = false;
            RemoveCameraButton.IsEnabled = false;
            cameraNameTextBox = new TextBox();
            cameraAddressTextBox = new TextBox();
            saveButton = new Button { Content = "Сохранить" };
            cancelButton = new Button { Content = "Отмена" };
            saveButton.Click += SaveCameraButton_Click;
            cancelButton.Click += CancelCameraButton_Click;

            CameraFieldsStackPanel.Children.Add(cameraNameTextBox);
            CameraFieldsStackPanel.Children.Add(cameraAddressTextBox);
            CameraFieldsStackPanel.Children.Add(saveButton);
            CameraFieldsStackPanel.Children.Add(cancelButton);
        }

        public void HideCameraFields()
        {
            // Удаление всех элементов из StackPanel
            CameraFieldsStackPanel.Children.Clear();
            AddCameraButton.IsEnabled = true;
            RemoveCameraButton.IsEnabled = true;
        }

        private void RemoveCameraButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный элемент из списка камер
            Camera selectedCamera = CameraList.SelectedItem as Camera;

            // Проверяем, что элемент действительно выбран
            if (selectedCamera != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure?",
                                                   "Remove confirmation",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Question);

                // Проверяем результат диалога
                if (result == MessageBoxResult.Yes)
                {
                    // Удаляем выбранную камеру из коллекции
                    Cameras.Remove(selectedCamera);
                }
            }
        }
    }
}
