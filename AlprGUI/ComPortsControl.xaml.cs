using AppDomain;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Camera = AppDomain.Camera;

namespace AlprGUI
{
    public partial class ComPortsControl : UserControl
    {
        private readonly ComPortsManager comPortManager;

        public ObservableCollection<ComPortPair> Pairs { get; set; }
        private AppSettings appSettings { get; set; }
        
        public ComPortsControl()
        {
            InitializeComponent();
            comPortManager = new ComPortsManager(new ModemEmulatorService());
            Pairs = new ObservableCollection<ComPortPair>();
            ComPortsList.ItemsSource = Pairs;
            appSettings = ConfigurationLoader.LoadSettings();
            ChangeVisibility();
            LoadPairsFromSettings();
        }

        private void AddPairButton_Click(object sender, RoutedEventArgs e)
        {
            var comForm = new ComPortPairForm();
            comForm.SaveClicked += SavePairButton_Click;
            comForm.CancelClicked += ComPortsPairForm_CancelClicked;
            HideCameraFields();
            PairFieldsStackPanel.Children.Add(comForm);
        }

        private void EditPairButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComPortsList.SelectedItem is ComPortPair selectedPair)
            {
                var comForm = new ComPortPairForm();
                comForm.SaveClicked += SavePairButton_Click;
                comForm.CancelClicked += ComPortsPairForm_CancelClicked;
                var pair = ComPortsList.SelectedItem as ComPortPair;
                comForm.Pair = pair;
                comForm.DataContext = pair;
                PairFieldsStackPanel.Children.Add(comForm);
                HideCameraFields();              
            }
        }

        private async void SavePairButton_Click(object sender, ComPortPairEventArgs e)
        {
            if (sender is ComPortPairForm comForm)
            {
                comForm.SaveClicked -= SavePairButton_Click;
                comForm.CancelClicked -= ComPortsPairForm_CancelClicked;
                ChangeVisibility();
                comForm.SaveClicked -= SavePairButton_Click;
                comForm.CancelClicked -= ComPortsPairForm_CancelClicked;
                PairFieldsStackPanel.Children.Remove(comForm);
                try
                {
                    await comPortManager.AddPair(e.Pair);
                }
                catch (Exception ex)
                {
                    MessageBoxResult result = MessageBox.Show(ex.Message);
                }
                LoadPairsFromSettings();
            }
        }

        public void HideCameraFields()
        {
            AddPairButton.IsEnabled = false;
            EditPairButton.IsEnabled = false;
            RemovePairButton.IsEnabled = false;
            ComPortsList.IsEnabled = false;
        }

        private void RemovePairButton_Click(object sender, RoutedEventArgs e)
        {
            ComPortPair selectedPair = ComPortsList.SelectedItem as ComPortPair;

            if (selectedPair != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure?",
                                                   "Remove confirmation",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    comPortManager.RemovePair(selectedPair.Name);
                    Pairs.Remove(selectedPair);
                }
            }
        }

        private void ComPortsPairForm_CancelClicked(object sender, EventArgs e)
        {
            if (sender is ComPortPairForm pairForm)
            {
                pairForm.SaveClicked -= SavePairButton_Click;
                pairForm.CancelClicked -= ComPortsPairForm_CancelClicked;
                PairFieldsStackPanel.Children.Remove(pairForm);
                ChangeVisibility();
            }
        }

        private void ComPortsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeVisibility();
        }

        private void ChangeVisibility()
        {
            bool isSelected = ComPortsList.SelectedItem != null;
            EditPairButton.IsEnabled = isSelected;
            RemovePairButton.IsEnabled = isSelected;
            AddPairButton.IsEnabled = true;
            ComPortsList.IsEnabled = true;
        }

        private void LoadPairsFromSettings()
        {
            Pairs.Clear();
            appSettings = ConfigurationLoader.LoadSettings();
            if (appSettings?.Cameras != null)
            {
                foreach (var pair in appSettings.ComPortPairs)
                {
                    Pairs.Add(pair);
                }
            }
        }
    }
}
