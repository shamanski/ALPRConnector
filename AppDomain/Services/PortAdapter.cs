﻿using AppDomain;
using AppDomain.Abstractions;
using Emgu.CV;
using System;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using openalprnet;
using System.Threading;
using Serilog;

public class PortAdapter : IDisposable
{
    private readonly ComPortService _comPortService;
    private readonly LprReader _reader;
    private readonly string _portName;
    private readonly int _rs485Address;
    private readonly CameraRepository cameraManager;
    private readonly LprReaderRepository readerManager;
    private readonly OpenAlprService alprClient;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public PortAdapter(ComPortService comPortService, LprReader reader)
    {
        _comPortService = comPortService;
        _reader = reader;
        cameraManager = new CameraRepository();
        readerManager = new LprReaderRepository();
        alprClient = new OpenAlprService();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task Run()
    {

        try
        {
            var connection = cameraManager.GetConnectionString(_reader.Camera);
            Log.Information($"Trying connect to {connection}");
            var videoCapture = await alprClient.CreateVideoCaptureAsync(connection, _cancellationTokenSource.Token);
            Log.Information($"Connected to {connection}");
            await alprClient.StartProcessingAsync(videoCapture, async result =>
            {
                await _comPortService.SendLpAsync(_reader.ComPortPair.Receiver, _reader.RS485Addr, result);
            },
            _cancellationTokenSource.Token
            );
        }
        catch (Exception ex)
        {
            Log.Error($"Connection unsuccessful");
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
        _comPortService.RemoveRS485Address(_rs485Address);
        SerialPortManager.CloseSerialPort(_portName);
    }
}
