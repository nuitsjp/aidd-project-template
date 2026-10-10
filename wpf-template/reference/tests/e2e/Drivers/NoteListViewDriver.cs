using System.Linq;
using System.Windows.Controls;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.TestAssistant.GeneratorToolKit;
using RM.Friendly.WPFStandardControls;

namespace WpfNotesSample.E2eTests.Drivers;

[UserControlDriver(TypeFullName = "WpfNotesSample.View.NoteListView")]
public sealed class NoteListViewDriver : IAppVarOwner
{
    public AppVar AppVar { get; }
    public WPFButtonBase NewNote => new(AppVar.Dynamic().NewNoteButton);
    public WPFButtonBase Refresh => new(AppVar.Dynamic().RefreshButton);
    public WPFTextBlock Error => new(AppVar.Dynamic().ErrorText);
    public WPFDataGrid Notes => new(AppVar.Dynamic().NotesGrid);

    public NoteListViewDriver(AppVar appVar) => AppVar = appVar;

    public WPFButtonBase ShowDetails(int index) => new(Notes.GetCell(index, 2).VisualTree().ByType<Button>().Single());

    public string[] Titles
    {
        get
        {
            var grid = Notes;
            return Enumerable.Range(0, grid.ItemCount).Select(index => grid.GetCellText(index, 0)).ToArray();
        }
    }
}
