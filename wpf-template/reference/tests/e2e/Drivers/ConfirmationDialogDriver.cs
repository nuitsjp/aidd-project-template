using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows.Grasp;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;
using WpfNotesSample.E2eTests.Support;

namespace WpfNotesSample.E2eTests.Drivers;

[WindowDriver(TypeFullName = "WpfNotesSample.View.ConfirmDialog")]
public sealed class ConfirmationDialogDriver : IAppVarOwner
{
    public WindowControl Core { get; }
    public AppVar AppVar => Core.AppVar;
    public WPFButtonBase Ok => new(AppVar.Dynamic().OkButton);
    public WPFButtonBase Cancel => new(AppVar.Dynamic().CancelButton);
    public string Title => new WPFTextBlock(AppVar.Dynamic().TitleText).Text;
    public string Message => new WPFTextBlock(AppVar.Dynamic().MessageText).Text;

    public ConfirmationDialogDriver(WindowControl core) => Core = core;

    public void WaitForClosed(Async operation)
    {
        UiWait.Until(() => !Core.IsWindow(), "確認ダイアログが閉じません。");
        UiWait.Until(() => operation.IsCompleted, "確認ダイアログを開いた操作が完了しません。");
        operation.WaitForCompletion();
    }
}
