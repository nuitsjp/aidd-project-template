using System.IO;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.Runtime;

public sealed class MockModeTests
{
    private readonly ITestOutputHelper output;
    public MockModeTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void MockEditIsVisibleUntilExitAndDoesNotCreateADatabase()
    {
        using var app = new AppSession(output, isMock: true);
        app.Main.Mode.Text.ShouldBe("モック：変更は終了時に破棄されます");
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "モックの固定データが表示されません。");
        list.Notes.EmulateChangeCurrentCell(0, 0);
        UiWait.Until(() => list.EditNote.IsEnabled, "モックのメモを編集できません。");
        list.EditNote.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.EmulateChangeText("モックで編集したメモ");
        editor.Body.EmulateChangeText("保存しても再起動時に破棄する本文");
        editor.Save.EmulateClick();
        UiWait.Until(() => editor.Status.Text == "保存しました。", "モックの保存が完了しません。");
        UiWait.Until(() => editor.Back.IsEnabled, "モックの保存後に一覧へ戻れません。");
        editor.Back.EmulateClick();
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "モックの編集後に一覧が表示されません。");
        list.Titles.ShouldContain("モックで編集したメモ");
        File.Exists(Path.Combine(app.DataDirectory, "notes.db")).ShouldBeFalse();
        app.Capture("mock-edited");
        app.Close();
        app.Start();

        app.Main.Mode.Text.ShouldBe("モック：変更は終了時に破棄されます");
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "再起動後にモックの固定データが表示されません。");
        list.Titles.ShouldBe(new[] { "はじめてのメモ", "確認して一括登録" }, ignoreOrder: true);
        File.Exists(Path.Combine(app.DataDirectory, "notes.db")).ShouldBeFalse();
        app.Close();
    }
}
