using System;
using System.IO;

namespace WpfNotesSample;

public sealed class ApplicationOptions
{
    public const string AppId = "WpfNotesSample";
    public string DataDirectory { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId);
    public bool IsMock { get; private set; }

    public static ApplicationOptions Parse(string[] args)
    {
        var options = new ApplicationOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data-dir" when i + 1 < args.Length:
                    options.DataDirectory = Path.GetFullPath(args[++i]);
                    break;
                case "--mock":
#if MOCK
                    options.IsMock = true;
                    break;
#else
                    throw new ArgumentException("モックは mise run dev:mock から起動してください。");
#endif
                default:
                    throw new ArgumentException("使用方法: App.exe [--data-dir <保存先>]");
            }
        }
#if MOCK
        if (!options.IsMock) throw new ArgumentException("Mock構成では --mock を指定してください。");
#endif
        return options;
    }
}
