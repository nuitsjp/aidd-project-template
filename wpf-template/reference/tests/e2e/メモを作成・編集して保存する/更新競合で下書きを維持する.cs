using System.Linq;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.EditNotes;

public sealed class PreserveDraftOnConflictTests
{
    private readonly ITestOutputHelper output;
    public PreserveDraftOnConflictTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void SavingAnOutdatedVersionPreservesTheDraftAndTheNewerSavedNote()
    {
        // 分岐条件: 編集画面が保持する版より新しい版がDBに保存されている。
        using var app = new AppSession(output);
        app.Database.ExecuteSql("""
            INSERT INTO notes VALUES ('11111111-1111-1111-1111-111111111111',
                '編集前のタイトル', '編集前の本文', 1, '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && list.EditNote.IsEnabled, "保存済みメモが表示されません。");
        list.Notes.EmulateChangeCurrentCell(0, 0);
        list.EditNote.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.EmulateChangeText("保持する下書きのタイトル");
        editor.Body.EmulateChangeText("保持する下書きの本文");
        app.Database.ExecuteSql("""
            UPDATE notes SET title = '他の操作で更新したタイトル', body = '新しい版の本文', version = 2;
            """);

        // 手順1: 下書きの保存を指示すると競合を表示し、確定しない。
        editor.Save.EmulateClick();
        UiWait.Until(() => !string.IsNullOrEmpty(editor.Error.Text), "更新競合が表示されません。");
        editor.Error.Text.ShouldBe("対象のメモは更新または削除されています。一覧を読み直してください。");
        editor.Status.Text.ShouldBeEmpty();

        // 受け入れ条件: 新しい版を上書きせず、入力したタイトルと本文を保持する。
        editor.Title.Text.ShouldBe("保持する下書きのタイトル");
        editor.Body.Text.ShouldBe("保持する下書きの本文");
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("他の操作で更新したタイトル", "新しい版の本文", 2));
        app.Capture("version-conflict-keeps-draft");
        UiWait.Until(() => editor.Discard.IsEnabled, "競合後に下書きを破棄できません。");
        editor.Discard.EmulateClick();
        app.Close();
    }
}
