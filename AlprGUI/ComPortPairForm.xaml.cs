using AppDomain;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AlprGUI
{
    public partial class ComPortPairForm : UserControl
    {
        public event EventHandler<ComPortPairEventArgs> SaveClicked;
        public event EventHandler CancelClicked;
        public ComPortPair Pair { get; set; }
        private readonly ComPortsManager portsManager;
        public ObservableCollection<string> FreePorts { get; set; }

        public ComPortPairForm()
        {
            InitializeComponent();
            this.DataContext = Pair;
            portsManager = new ComPortsManager();
            FreePorts = new ObservableCollection<string>();
            LoadPairs();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var pair = new ComPortPair
            {
                Sender = senderComboBox.SelectedItem.ToString(),
                Receiver = receiverComboBox.SelectedItem.ToString(),
            };

            var context = new ValidationContext(pair, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(pair, context, results, true);

            if (isValid)
            {
                // If the model is valid, invoke the SaveClicked event
                SaveClicked?.Invoke(this, new ComPortPairEventArgs(pair));
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

        private void LoadPairs()
        {
            FreePorts.Clear();
            var ports = portsManager.GetFreePorts();
                foreach (var port in ports)
                {
                    FreePorts.Add(port);
                }
        }
    }

    public class ComPortPairEventArgs : EventArgs
    {
        public ComPortPair Pair { get; }

        public ComPortPairEventArgs(ComPortPair pair)
        {
            Pair = pair;
        }
    }


}
