using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;

namespace WpfNotesSample.E2eTests.Drivers;

[UserControlDriver(TypeFullName = "WpfNotesSample.View.ImportNotesView")]
public sealed class ImportNotesViewDriver : IAppVarOwner
{
    public AppVar AppVar { get; }
    public WPFTextBox Input => new(AppVar.Dynamic().InputTextBox);
    public WPFButtonBase Preview => new(AppVar.Dynamic().PreviewButton);
    public WPFButtonBase Confirm => new(AppVar.Dynamic().ConfirmButton);
    public WPFDataGrid PreviewItems => new(AppVar.Dynamic().PreviewGrid);
    public WPFTextBlock Error => new(AppVar.Dynamic().ErrorText);
    public WPFTextBlock Status => new(AppVar.Dynamic().StatusText);

    public ImportNotesViewDriver(AppVar appVar) => AppVar = appVar;
}
