using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WpfNotesSample.Domain.Notes;
#if MOCK
using WpfNotesSample.Infrastructure.InMemory.Notes;
#endif
using WpfNotesSample.Infrastructure.Sqlite;
using WpfNotesSample.Infrastructure.Sqlite.Notes;
using WpfNotesSample.View;
using WpfNotesSample.ViewModel;

namespace WpfNotesSample;

public partial class App : Application
{
    private const int StartupFailureExitCode = 1;
    private const int AlreadyRunningExitCode = 2;

    public App() => InitializeComponent();

    [STAThread]
    [SuppressMessage("Minor Code Smell", "S2221", Justification = "起動の失敗はすべて原因を表示して終了する受け口。")]
    public static void Main(string[] args)
    {
        try
        {
            var options = ApplicationOptions.Parse(args);
            using var sha = SHA256.Create();
            var identity = options.DataDirectory.ToUpperInvariant() + options.IsMock;
            var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
            using var instance = new Mutex(true, @"Local\" + ApplicationOptions.AppId + "." + hash, out var isFirst);
            if (!isFirst)
            {
                MessageBox.Show("この保存先を使用するアプリは既に起動しています。", "起動済み");
                Environment.ExitCode = AlreadyRunningExitCode;
                return;
            }

            var builder = KamishibaiApplication<App, MainWindow>.CreateBuilder();
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<NavigationState>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
#if MOCK
            builder.Services.AddSingleton<INotesService, InMemoryNotesService>();
#else
            var database = new Database(Path.Combine(options.DataDirectory, "notes.db"));
            database.Initialize();
            builder.Services.AddSingleton(database);
            builder.Services.AddSingleton<INotesService, NotesService>();
#endif
            builder.Services.AddPresentation<MainWindow, MainViewModel>();
            builder.Services.AddPresentation<NoteListView, NoteListViewModel>();
            builder.Services.AddPresentation<NoteEditView, NoteEditViewModel>();
            builder.Services.AddPresentation<NoteDetailsView, NoteDetailsViewModel>();
            using var host = builder.Build();
            host.RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(error.Message, "アプリを起動できません", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Environment.ExitCode = StartupFailureExitCode;
        }
    }
}
