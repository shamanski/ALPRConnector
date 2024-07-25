using System.Diagnostics;
using System.IO;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Windows;
using AppDomain;
using Microsoft.Win32.TaskScheduler;
using Serilog;
using Serilog.Sinks.RichTextBox.Themes;
using Application = System.Windows.Application;
using Task = System.Threading.Tasks.Task;
namespace AlprGUI;

public partial class App : Application
{
    private LogBox logControl;
    private static Mutex mutex = new Mutex(true, "{YourAppMutexGUID}");

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexId = "{YourAppMutexGUID}";
        bool createdNew;
        mutex = new Mutex(true, mutexId, out createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("An instance of the application is already running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        AddToStartup();
        logControl = new LogBox();
        var loggerConfig = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .WriteTo.RichTextBox(logControl.LogRichTextBox, theme: RichTextBoxConsoleTheme.Colored);
        var logger = loggerConfig.CreateLogger();
        Log.Logger = logger;
        var lprReaders = LoadLprReaders();
        var tasks = new List<Task>();
        foreach (var reader in lprReaders)
        {
            if (reader.AutoStart)
            {
                tasks.Add(Task.Run(() => PortAdapterManager.Instance.StartAdapterAsync(reader.LprReader)));
            }           
        }
        var mainWindow = new MainWindow(logControl);
        mainWindow.Show();
        await Task.WhenAll(tasks);
        Log.Information("All services started.");

    }

    private static void AddToStartup()
    {
        using (TaskService ts = new TaskService())
        {
            TaskDefinition td = ts.NewTask();
            td.RegistrationInfo.Description = "Your Application Description";
            td.Principal.RunLevel = TaskRunLevel.Highest; // Запуск от имени администратора

            td.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromSeconds(10) });

            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            td.Actions.Add(new ExecAction(exePath, null, Path.GetDirectoryName(exePath)));

            ts.RootFolder.RegisterTaskDefinition("YourAppName", td);
        }
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