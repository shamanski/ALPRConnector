using AppDomain;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AlprGUI
{
    public partial class LprReadersControl : UserControl
    {
        private readonly LprReaderRepository lprReaderManager;

        public ObservableCollection<LprReader> LprReaders { get; set; }
        private AppSettings appSettings { get; set; }

        public LprReadersControl()
        {
            InitializeComponent();
            lprReaderManager = new LprReaderRepository();
            LprReaders = new ObservableCollection<LprReader>();
            LprReadersList.ItemsSource = LprReaders;
            appSettings = ConfigurationLoader.LoadSettings();
            ChangeVisibility();
            LoadReadersFromSettings();
        }

        private void AddReaderButton_Click(object sender, RoutedEventArgs e)
        {
            var readerForm = new LprReaderForm();
            readerForm.SaveClicked += SaveReaderButton_Click;
            readerForm.CancelClicked += ReaderForm_CancelClicked;
            HideReaderFields();
            ReaderFieldsStackPanel.Children.Add(readerForm);
        }

        private void EditReaderButton_Click(object sender, RoutedEventArgs e)
        {
            if (LprReadersList.SelectedItem is LprReader selectedReader)
            {
                var readerForm = new LprReaderForm();
                readerForm.SaveClicked += SaveReaderButton_Click;
                readerForm.CancelClicked += ReaderForm_CancelClicked;
                readerForm.Reader = selectedReader;
                readerForm.DataContext = selectedReader;
                ReaderFieldsStackPanel.Children.Add(readerForm);
                HideReaderFields();
            }
        }

        private void SaveReaderButton_Click(object sender, LprReaderEventArgs e)
        {
            if (sender is LprReaderForm readerForm)
            {
                readerForm.SaveClicked -= SaveReaderButton_Click;
                readerForm.CancelClicked -= ReaderForm_CancelClicked;
                ChangeVisibility();
                ReaderFieldsStackPanel.Children.Remove(readerForm);
                try
                {
                    var existingReader = LprReaders.FirstOrDefault(r => r.Name == e.Reader.Name);
                    if (existingReader != null)
                    {
                        // Update existing reader
                        existingReader.Name = e.Reader.Name;
                        existingReader.ComPortPair = e.Reader.ComPortPair;
                        existingReader.Camera = e.Reader.Camera;
                    }
                    else
                    {
                        // Add new reader
                        LprReaders.Add(e.Reader);
                    }

                    lprReaderManager.AddReader(e.Reader);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                LoadReadersFromSettings();
            }
        }

        public void HideReaderFields()
        {
            AddReaderButton.IsEnabled = false;
            EditReaderButton.IsEnabled = false;
            RemoveReaderButton.IsEnabled = false;
            LprReadersList.IsEnabled = false;
        }

        private void RemoveReaderButton_Click(object sender, RoutedEventArgs e)
        {
            LprReader selectedReader = LprReadersList.SelectedItem as LprReader;

            if (selectedReader != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure?",
                                                   "Remove confirmation",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    lprReaderManager.RemoveReader(selectedReader.Name);
                    LprReaders.Remove(selectedReader);
                }
            }
        }

        private void ReaderForm_CancelClicked(object sender, EventArgs e)
        {
            if (sender is LprReaderForm readerForm)
            {
                readerForm.SaveClicked -= SaveReaderButton_Click;
                readerForm.CancelClicked -= ReaderForm_CancelClicked;
                ReaderFieldsStackPanel.Children.Remove(readerForm);
                ChangeVisibility();
            }
        }

        private void LprReadersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeVisibility();
        }

        private void ChangeVisibility()
        {
            bool isSelected = LprReadersList.SelectedItem != null;
            EditReaderButton.IsEnabled = isSelected;
            RemoveReaderButton.IsEnabled = isSelected;
            AddReaderButton.IsEnabled = true;
            LprReadersList.IsEnabled = true;
        }

        private void LoadReadersFromSettings()
        {
            LprReaders.Clear();
            appSettings = ConfigurationLoader.LoadSettings();
            if (appSettings?.LprReaders != null)
            {
                foreach (var reader in appSettings.LprReaders)
                {
                    LprReaders.Add(reader);
                }
            }
        }
    }
}
