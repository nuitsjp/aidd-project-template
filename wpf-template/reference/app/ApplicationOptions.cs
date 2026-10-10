using System;
using System.Collections.Generic;
using System.IO;

namespace WpfNotesSample;

public sealed class ApplicationOptions
{
    private ApplicationOptions(string dataDirectory, bool isMock)
    {
        DataDirectory = dataDirectory;
        IsMock = isMock;
    }

    public static string AppId { get; } = "WpfNotesSample";
    public string DataDirectory { get; }
    public bool IsMock { get; }

    public static ApplicationOptions Parse(string[] args)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId);
        var mockRequested = false;
        var remaining = new Queue<string>(args);
        while (remaining.Count > 0)
        {
            var argument = remaining.Dequeue();
            if (argument == "--data-dir" && remaining.Count > 0)
            {
                dataDirectory = Path.GetFullPath(remaining.Dequeue());
            }
            else if (argument == "--mock")
            {
                mockRequested = true;
            }
            else
            {
                throw new ArgumentException("使用方法: App.exe [--data-dir <保存先>]");
            }
        }
#if MOCK
        if (!mockRequested)
        {
            throw new ArgumentException("Mock構成では --mock を指定してください。");
        }
#else
        if (mockRequested)
        {
            throw new ArgumentException("モックは mise run dev:mock から起動してください。");
        }
#endif
        return new ApplicationOptions(dataDirectory, mockRequested);
    }
}
