using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Windows;
using AppDomain;
using Serilog;
using Serilog.Sinks.RichTextBox.Themes;
namespace AlprGUI;

public partial class App : Application
{
    private LogBox logControl;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        logControl = new LogBox();
        var loggerConfig = new LoggerConfiguration()
               .WriteTo.RichTextBox(logControl.LogRichTextBox, theme: RichTextBoxConsoleTheme.Colored);
        var logger = loggerConfig.CreateLogger();
        Log.Logger = logger;
        var lprReaders = LoadLprReaders();
        var tasks = new List<Task>();
        foreach (var reader in lprReaders)
            {
            tasks.Add(Task.Run(() => PortAdapterManager.Instance.StartAdapterAsync(reader.LprReader)));
        }
        var mainWindow = new MainWindow(logControl);
        mainWindow.Show();
        await Task.WhenAll(tasks);
        Log.Information("All services started.");

    }

    private List<LprReaderViewModel> LoadLprReaders()
    {
        var readerManager = new LprReaderRepository();
        var lprs = new List<LprReaderViewModel>();
        var readers = readerManager.GetAll();
        foreach (var reader in readers)
        {
            lprs.Add(new LprReaderViewModel(reader));
        }

        return lprs;
    }
}