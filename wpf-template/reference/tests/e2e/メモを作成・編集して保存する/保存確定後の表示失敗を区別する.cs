using System.Linq;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.EditNotes;

public sealed class DistinguishDisplayFailureAfterSaveTests
{
    private readonly ITestOutputHelper output;
    public DistinguishDisplayFailureAfterSaveTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void FailingToLoadTheListDoesNotUndoOrRepeatTheCompletedSave()
    {
        // 分岐条件: 保存完了後、一覧へ戻ったときのDB読み取りが失敗する。
        using var app = new AppSession(output);
        app.Main.NoteList.NewNote.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.EmulateChangeText("確定したタイトル");
        editor.Body.EmulateChangeText("確定した本文");
        editor.Save.EmulateClick();
        UiWait.Until(() => editor.Status.Text == "保存しました。" && editor.Back.IsEnabled,
            "保存が完了しません。");
        var savedId = app.Database.ReadNoteId("確定したタイトル");
        app.Database.ExecuteSql("ALTER TABLE notes RENAME TO saved_notes;");

        // 手順1: 一覧へ戻ると取得失敗を表示し、完了済みの保存を成功のまま扱う。
        editor.Back.EmulateClick();
        var list = app.Main.NoteList;
        UiWait.Until(() => !string.IsNullOrEmpty(list.Error.Text), "一覧の取得失敗が表示されません。");
        list.Error.Text.ShouldContain("no such table: notes");
        editor.Status.Text.ShouldBe("保存しました。");
        editor.Error.Text.ShouldBeEmpty();

        // 受け入れ条件: 保存済みの値と版が残り、取得の再試行で二重保存しない。
        app.Database.ExecuteSql("ALTER TABLE saved_notes RENAME TO notes;");
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("確定したタイトル", "確定した本文", 1));
        app.Database.ReadNoteId("確定したタイトル").ShouldBe(savedId);
        UiWait.Until(() => list.Refresh.IsEnabled, "一覧を再取得できません。");
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && string.IsNullOrEmpty(list.Error.Text),
            "復旧後の一覧が表示されません。");
        list.Notes.GetCellText(0, 0).ShouldBe("確定したタイトル");
        app.Close();
        app.Start();
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 1, "再起動後に確定済みのメモが表示されません。");
        app.Database.ReadNotes().Single().Version.ShouldBe(1);
        app.Close();
    }
}
