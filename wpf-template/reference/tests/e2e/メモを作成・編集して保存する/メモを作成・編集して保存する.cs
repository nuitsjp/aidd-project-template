using System;
using System.Linq;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.EditNotes;

public sealed class SaveNoteScenarioTests
{
    private readonly ITestOutputHelper output;

    public SaveNoteScenarioTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveNewOrExistingNotePersistsConfirmedValues(bool editExisting)
    {
        // 開始条件
        using var app = new AppSession(output);
        var existingId = Guid.Parse("b90b0486-a6bf-49e8-bf87-b90c0d18d3ac");
        if (editExisting)
        {
            app.Database.ExecuteSql("""
                INSERT INTO notes (id, title, body, version, updated_at)
                VALUES ('b90b0486-a6bf-49e8-bf87-b90c0d18d3ac', '編集前のメモ', '編集前の本文', 1,
                    '2026-01-01T00:00:00.0000000Z');
                """);
        }

        // 手順1
        app.Main.ShowNotes.EmulateClick();
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == (editExisting ? 1 : 0),
            "開始条件のメモ一覧が表示されません。");

        // 手順2
        if (editExisting)
        {
            list.Notes.EmulateChangeCurrentCell(0, 0);
            list.EditNote.EmulateClick();
        }
        else
        {
            list.NewNote.EmulateClick();
        }
        var editor = app.Main.NoteEdit;
        const string title = "保存したメモ";
        const string body = "保存する本文\r\n改行後の本文";
        editor.Title.EmulateChangeText("  " + title + "　");
        editor.Body.EmulateChangeText(body);

        // 手順3
        editor.Save.EmulateClick();
        UiWait.Until(() => editor.Status.Text == "保存しました。", "メモの保存が完了しません。");
        var confirmedTitle = editor.Title.Text;
        var confirmedBody = editor.Body.Text;
        var saveStatus = editor.Status.Text;
        UiWait.Until(() => editor.Back.IsEnabled, "保存後に一覧へ戻れません。");

        // 手順4
        editor.Back.EmulateClick();
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 1, "保存したメモが一覧に表示されません。");

        // 受け入れ条件
        var expected = new SavedNote(title, body, editExisting ? 2 : 1);
        app.Database.ReadNotes().Single().ShouldBe(expected);
        var savedId = app.Database.ReadNoteId(title);
        savedId.ShouldNotBe(Guid.Empty);
        if (editExisting) savedId.ShouldBe(existingId);
        confirmedTitle.ShouldBe(title);
        confirmedBody.ShouldBe(body);
        saveStatus.ShouldBe("保存しました。");
        list.Titles.ShouldBe(new[] { title });
        list.Notes.GetCellText(0, 1).ShouldBe(body);
        app.Capture(editExisting ? "note-updated" : "note-created");
        app.Close();
        app.Start();

        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 1, "再起動後に保存したメモが表示されません。");
        list.Titles.ShouldBe(new[] { title });
        app.Database.ReadNoteId(title).ShouldBe(savedId);
        app.Database.ReadNotes().Single().ShouldBe(expected);
        list.Notes.EmulateChangeCurrentCell(0, 0);
        list.EditNote.EmulateClick();
        editor = app.Main.NoteEdit;
        editor.Title.Text.ShouldBe(title);
        editor.Body.Text.ShouldBe(body);
        app.Close();
    }
}
