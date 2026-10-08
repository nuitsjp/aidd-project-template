using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;
using WpfNotesSample.E2eTests.Support;
using WpfNotesSample.View;

namespace WpfNotesSample.E2eTests.Drivers;

[WindowDriver(TypeFullName = "WpfNotesSample.View.MainWindow")]
public sealed class MainWindowDriver : IAppVarOwner
{
    public WindowControl Core { get; }
    public AppVar AppVar => Core.AppVar;
    public WPFTextBlock Mode => new(AppVar.Dynamic().ModeText);
    public WPFButtonBase ShowNotes => new(AppVar.Dynamic().ShowNotesButton);
    public WPFButtonBase ShowImport => new(AppVar.Dynamic().ShowImportButton);

    public MainWindowDriver(WindowControl core) => Core = core;
    public MainWindowDriver(AppVar appVar) : this(new WindowControl(appVar)) { }

    public NoteListViewDriver NoteList => new(WaitForView<NoteListView>());
    public NoteEditViewDriver NoteEdit => new(WaitForView<NoteEditView>());
    public ImportNotesViewDriver ImportNotes => new(WaitForView<ImportNotesView>());

    private AppVar WaitForView<T>() where T : System.Windows.DependencyObject
    {
        AppVar? view = null;
        UiWait.Until(() => (view = AppVar.VisualTree().ByType<T>().SingleOrDefault()) != null,
            $"{typeof(T).Name} が表示されません。");
        return view!;
    }

    public ConfirmationDialogDriver WaitForConfirmation(Async operation)
        => new(Core.WaitForNextModal(operation));
}

public static class MainWindowDriverExtensions
{
    [WindowDriverIdentify(TypeFullName = "WpfNotesSample.View.MainWindow")]
    public static MainWindowDriver AttachMainWindow(this WindowsAppFriend app)
        => new(app.WaitForIdentifyFromTypeFullName(typeof(MainWindow).FullName));
}
