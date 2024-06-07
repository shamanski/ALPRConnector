using Emgu.CV;
using System.IO.Ports;

namespace ConsoleApp1
{   
    internal class Program
    {

        private static void Process(string obj)
        {
            Console.WriteLine("Processing...");
        }

        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting...");
            var settings = ConfigurationLoader.LoadSettings();
            var modemEmulator = new ModemEmulator();
            await modemEmulator.Initialize();

            var client = new OpenAlprClient();
            var videoCapture = new VideoCapture(@"rtsp://admin:admin@192.168.107.166:8080");

            var sender = new SerialPort("COM10");
            var receiver = new SerialPort("COM11");
            sender.Open();
            receiver.Open();
            sender.WriteLine("HELLO COM");
            Console.WriteLine(receiver.ReadLine());
            var cancellationTokenSource = new CancellationTokenSource();
            var processTask = client.StartProcessingAsync(videoCapture, new Action<string>(Process), cancellationTokenSource.Token);

            Console.WriteLine("Press 'q' to quit.");
            while (Console.ReadKey().Key != ConsoleKey.Q) { }

            cancellationTokenSource.Cancel();
            await processTask;

            client.StopProcessing();
        }
        
    }
}
