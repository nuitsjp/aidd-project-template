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
    public WPFListBox Notes => new(AppVar.Dynamic().NotesList);

    public NoteListViewDriver(AppVar appVar) => AppVar = appVar;

    public WPFButtonBase ShowDetails(int index) => new(Notes.GetItem(index).VisualTree().ByType<Button>().Single());

    public string UpdatedAt(int index) => ItemText(Notes, index, "UpdatedText");

    public string[] Titles
    {
        get
        {
            var list = Notes;
            return Enumerable.Range(0, list.ItemCount).Select(index => ItemText(list, index, "TitleText")).ToArray();
        }
    }

    // 項目のテンプレート内で名前を付けた TextBlock の表示文字列を読む。
    private static string ItemText(WPFListBox list, int index, string name)
        => new WPFTextBlock(list.GetItem(index).VisualTree().ByType<TextBlock>().ByName(name).Single()).Text;
}
