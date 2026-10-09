using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;

namespace WpfNotesSample.E2eTests.Drivers;

[UserControlDriver(TypeFullName = "WpfNotesSample.View.NoteDetailsView")]
public sealed class NoteDetailsViewDriver : IAppVarOwner
{
    public AppVar AppVar { get; }
    public WPFTextBlock Title => new(AppVar.Dynamic().TitleText);
    public WPFTextBlock Body => new(AppVar.Dynamic().BodyText);
    public WPFTextBlock Error => new(AppVar.Dynamic().ErrorText);
    public WPFButtonBase Edit => new(AppVar.Dynamic().EditButton);
    public WPFButtonBase Delete => new(AppVar.Dynamic().DeleteButton);
    public WPFButtonBase Back => new(AppVar.Dynamic().BackButton);

    public NoteDetailsViewDriver(AppVar appVar) => AppVar = appVar;
}
