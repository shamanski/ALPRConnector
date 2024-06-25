using System;
using System.IO;
using System.Text;
using Serilog;

public class ConsoleInterceptor : TextWriter
{
    private TextWriter _originalOut;
    private TextWriter _originalError;

    public ConsoleInterceptor()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;

        Console.SetOut(this);
        Console.SetError(this);
    }

    public override void Write(char value)
    {
        Log.Information(value.ToString());
        _originalOut.Write(value);
    }

    public override void Write(string value)
    {
        Log.Information(value);
        _originalOut.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Log.Information(new string(buffer, index, count));
        _originalOut.Write(buffer, index, count);
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string value)
    {
        Log.Information(value);
        _originalOut.WriteLine(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);
        }
        base.Dispose(disposing);
    }
}

