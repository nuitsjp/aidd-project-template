using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;

namespace WpfNotesSample.E2eTests.Drivers;

[UserControlDriver(TypeFullName = "WpfNotesSample.View.NoteEditView")]
public sealed class NoteEditViewDriver : IAppVarOwner
{
    public AppVar AppVar { get; }
    public WPFTextBox Title => new(AppVar.Dynamic().TitleInput);
    public WPFTextBox Body => new(AppVar.Dynamic().BodyInput);
    public WPFButtonBase Save => new(AppVar.Dynamic().SaveButton);
    public WPFButtonBase Discard => new(AppVar.Dynamic().DiscardButton);
    public WPFButtonBase Back => new(AppVar.Dynamic().BackButton);
    public WPFTextBlock Error => new(AppVar.Dynamic().ErrorText);
    public WPFTextBlock Status => new(AppVar.Dynamic().StatusText);

    public NoteEditViewDriver(AppVar appVar) => AppVar = appVar;
}
