using System;
using System.Diagnostics;
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
    private readonly string tempBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WpfNotesSample-ui-tests"));
    private readonly ITestOutputHelper output;
    private readonly string executable;
    private readonly bool isMock;
    private Process? process;
    private WindowsAppFriend? application;

    public MainWindowDriver Main { get; private set; } = null!;
    public string DataDirectory { get; }
    public NoteDatabase Database { get; }

    public AppSession(ITestOutputHelper output, bool isMock = false)
    {
        this.output = output;
        this.isMock = isMock;
        executable = isMock
            ? Environment.GetEnvironmentVariable("WPF_MOCK_APP_PATH")
                ?? throw new InvalidOperationException("WPF_MOCK_APP_PATH に Mock 構成の実行ファイルを指定してください。")
            : Environment.GetEnvironmentVariable("WPF_APP_PATH")
                ?? Path.ChangeExtension(typeof(App).Assembly.Location, ".exe");
        File.Exists(executable).ShouldBeTrue($"対象の実行ファイルがありません: {executable}");
        DataDirectory = Path.Combine(tempBase, Guid.NewGuid().ToString("N"));
        Database = new NoteDatabase(DataDirectory);
        Directory.CreateDirectory(DataDirectory);
        try { Start(); }
        catch { Dispose(); throw; }
    }

    public void Start()
    {
        application?.Dispose();
        process?.Dispose();
        var arguments = $"--data-dir \"{DataDirectory}\"" + (isMock ? " --mock" : "");
        process = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
        })!;
        application = new WindowsAppFriend(process);
        Main = application.AttachMainWindow();
        UiWait.Until(() => Main.NoteList.NewNote.IsEnabled, "メモ一覧の初期表示が完了しません。");
    }

    public void Close()
    {
        Main.Core.Close(new Async());
        WaitForExit();
    }

    public void WaitForExit()
    {
        application?.Dispose();
        application = null;
        UiWait.Until(() => process!.HasExited, "アプリが終了しません。");
    }

    public void Capture(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WpfNotesSample-ui-screenshots");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Path.GetFileName(DataDirectory)}-{name}.png");
        WindowsAppExpander.LoadAssembly(application!, typeof(WindowScreenshot).Assembly);
        application!.Type(typeof(WindowScreenshot)).Capture(Main.Core.AppVar, path);
        output.WriteLine($"Screenshot: {path}");
    }

    public void Dispose()
    {
        if (process != null && !process.HasExited)
        {
            process.Kill();
            process.WaitForExit();
        }
        application?.Dispose();
        process?.Dispose();
        var resolved = Path.GetFullPath(DataDirectory);
        if (!resolved.StartsWith(tempBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("一時保存領域の外は削除できません。");
        Directory.Delete(resolved, recursive: true);
    }
}
