using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows;
using Shouldly;
using WpfNotesSample.E2eTests.Drivers;
using Xunit;

namespace WpfNotesSample.E2eTests.Support;

internal sealed class AppSession : IDisposable
{
    private readonly string _tempBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WpfNotesSample-ui-tests"));
    private readonly ITestOutputHelper _output;
    private readonly string _executable;
    private readonly bool _isMock;
    private Process? _process;
    private WindowsAppFriend? _application;

    public MainWindowDriver Main { get; private set; } = null!;
    public string DataDirectory { get; }
    public NoteDatabase Database { get; }

    public AppSession(ITestOutputHelper output, bool isMock = false)
    {
        _output = output;
        _isMock = isMock;
        _executable = _isMock
            ? Environment.GetEnvironmentVariable("WPF_MOCK_APP_PATH")
                ?? throw new InvalidOperationException("WPF_MOCK_APP_PATH に Mock 構成の実行ファイルを指定してください。")
            : Environment.GetEnvironmentVariable("WPF_APP_PATH")
                ?? Path.ChangeExtension(typeof(App).Assembly.Location, ".exe");
        File.Exists(_executable).ShouldBeTrue($"対象の実行ファイルがありません: {_executable}");
        DataDirectory = Path.Combine(_tempBase, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        // 後片付けで再帰削除するため、一時保存領域の中であることを作成時に確かめる。
        if (!Path.GetFullPath(DataDirectory).StartsWith(_tempBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("一時保存領域の外は削除できません。");
        }

        Database = new NoteDatabase(DataDirectory);
        Directory.CreateDirectory(DataDirectory);
        try { Start(); }
        catch { Dispose(); throw; }
    }

    public void Start()
    {
        _application?.Dispose();
        _process?.Dispose();
        var arguments = $"--data-dir \"{DataDirectory}\"" + (_isMock ? " --mock" : "");
        _process = Process.Start(new ProcessStartInfo(_executable, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_executable)!,
        })!;
        _application = new WindowsAppFriend(_process);
        Main = _application.AttachMainWindow();
        UiWait.Until(() => Main.NoteList.NewNote.IsEnabled, "ノート一覧の初期表示が完了しません。");
    }

    public void Close()
    {
        Main.Core.Close(new Async());
        WaitForExit();
    }

    public void WaitForExit()
    {
        _application?.Dispose();
        _application = null;
        UiWait.Until(() => _process!.HasExited, "アプリが終了しません。");
    }

    public void Capture(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WpfNotesSample-ui-screenshots");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Path.GetFileName(DataDirectory)}-{name}.png");
        WindowsAppExpander.LoadAssembly(_application!, typeof(WindowScreenshot).Assembly);
        _application!.Type(typeof(WindowScreenshot)).Capture(Main.Core.AppVar, path);
        _output.WriteLine($"Screenshot: {path}");
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit();
        }
        _application?.Dispose();
        _process?.Dispose();
        Directory.Delete(DataDirectory, recursive: true);
    }
}
