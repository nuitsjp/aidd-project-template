using Codeer.Friendly;
using Codeer.Friendly.Windows.Grasp;
using Codeer.Friendly.Windows.NativeStandardControls;
using WpfNotesSample.E2eTests.Support;

namespace WpfNotesSample.E2eTests.Drivers;

public sealed class ConfirmationDialogDriver : IAppVarOwner
{
    public WindowControl Core { get; }
    public AppVar AppVar => Core.AppVar;
    public NativeMessageBox MessageBox { get; }
    public NativeButton Ok => new(Core.IdentifyFromDialogId(1));
    public NativeButton Cancel => new(Core.IdentifyFromDialogId(2));
    public string Title => MessageBox.Title;
    public string Message => MessageBox.Message;

    public ConfirmationDialogDriver(WindowControl core)
    {
        Core = core;
        MessageBox = new NativeMessageBox(core);
    }

    public void WaitForClosed(Async operation)
    {
        UiWait.Until(() => !Core.IsWindow(), "確認ダイアログが閉じません。");
        UiWait.Until(() => operation.IsCompleted, "確認ダイアログを開いた操作が完了しません。");
        operation.WaitForCompletion();
    }
}
