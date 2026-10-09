using System.IO;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.Runtime;

public sealed class MockModeTests
{
    private readonly ITestOutputHelper _output;
    public MockModeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void MockEditIsVisibleUntilExitAndDoesNotCreateADatabase()
    {
        using var app = new AppSession(_output, isMock: true);
        app.Main.Mode.Text.ShouldBe("モック：変更は終了時に破棄されます");
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "モックの固定データが表示されません。");
        var originalTitles = list.Titles;
        list.ShowDetails(0).EmulateClick();
        var details = app.Main.NoteDetails;
        UiWait.Until(() => details.Edit.IsEnabled, "モックのノートを編集できません。");
        details.Edit.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.EmulateChangeText("モックで編集したノート");
        editor.Body.EmulateChangeText("保存しても再起動時に破棄する本文");
        editor.Save.EmulateClick();
        UiWait.Until(() => editor.Status.Text == "保存しました。", "モックの保存が完了しません。");
        UiWait.Until(() => editor.Back.IsEnabled, "モックの保存後に一覧へ戻れません。");
        editor.Back.EmulateClick();
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "モックの編集後に一覧が表示されません。");
        list.Titles.ShouldContain("モックで編集したノート");
        File.Exists(Path.Combine(app.DataDirectory, "notes.db")).ShouldBeFalse();
        app.Capture("mock-edited");
        app.Close();
        app.Start();

        app.Main.Mode.Text.ShouldBe("モック：変更は終了時に破棄されます");
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "再起動後にモックの固定データが表示されません。");
        list.Titles.ShouldBe(originalTitles, ignoreOrder: true);
        File.Exists(Path.Combine(app.DataDirectory, "notes.db")).ShouldBeFalse();
        app.Close();
    }
}
