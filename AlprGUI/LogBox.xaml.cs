using Serilog.Core;
using Serilog.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Serilog.Sinks.RichTextBox.Themes;

namespace AlprGUI
{
    public partial class LogBox : UserControl
    {
        private LoggerConfiguration _loggerConfig;
        private LoggingLevelSwitch _loggingLevelSwitch;
        public LogBox()
        {
            InitializeComponent();
            LogRichTextBox.TextChanged += LogRichTextBox_TextChanged;
            _loggingLevelSwitch = new LoggingLevelSwitch();
            _loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_loggingLevelSwitch)
                .WriteTo.RichTextBox(LogRichTextBox, theme: RichTextBoxConsoleTheme.Colored);
            Log.Logger = _loggerConfig.CreateLogger();
            _loggingLevelSwitch.MinimumLevel = LogEventLevel.Information;
        }
        private void DebugCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _loggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
            Log.Debug("Debug logging enabled.");
        }

        private void DebugCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _loggingLevelSwitch.MinimumLevel = LogEventLevel.Information;
            Log.Information("Debug logging disabled.");
        }

        private void LogRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogRichTextBox.ScrollToEnd();
        }


    }
}
