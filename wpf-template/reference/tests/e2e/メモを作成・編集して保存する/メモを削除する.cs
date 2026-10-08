using System;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.EditNotes;

public sealed class DeleteNoteScenarioTests
{
    private readonly ITestOutputHelper output;

    public DeleteNoteScenarioTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelKeepsNotesAndDeleteChecksTheSelectedVersion(bool changeVersion)
    {
        // 分岐条件
        using var app = new AppSession(output);
        app.Database.ExecuteSql("""
            INSERT INTO notes (id, title, body, version, updated_at)
            VALUES ('b90b0486-a6bf-49e8-bf87-b90c0d18d3ac', '削除するメモ', '削除対象の本文', 1,
                '2026-01-02T00:00:00.0000000Z'),
                ('d3115922-5803-4926-b902-d9c9572d8bc0', '保持するメモ', '保持する本文', 1,
                '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 2, "削除対象と保持するメモが一覧に表示されません。");
        list.Notes.EmulateChangeCurrentCell(Array.IndexOf(list.Titles, "削除するメモ"), 0);
        UiWait.Until(() => list.DeleteNote.IsEnabled, "保存済みのメモを削除できません。");

        // 手順1
        var cancelDelete = new Async();
        list.DeleteNote.EmulateClick(cancelDelete);
        var confirmation = app.Main.WaitForConfirmation(cancelDelete);
        confirmation.Title.ShouldBe("メモの削除");
        confirmation.Message.ShouldBe("「削除するメモ」を削除しますか？");

        // 手順2
        confirmation.Cancel.EmulateClick();
        confirmation.WaitForClosed(cancelDelete);
        var notesAfterCancel = app.Database.ReadNotes();
        var titlesAfterCancel = list.Titles;
        var targetIdAfterCancel = app.Database.ReadNoteId("削除するメモ");
        var remainingIdAfterCancel = app.Database.ReadNoteId("保持するメモ");

        // 手順3
        if (changeVersion)
        {
            app.Database.ExecuteSql("""
                UPDATE notes SET version = version + 1
                WHERE id = 'b90b0486-a6bf-49e8-bf87-b90c0d18d3ac';
                """);
        }
        var acceptDelete = new Async();
        list.DeleteNote.EmulateClick(acceptDelete);
        confirmation = app.Main.WaitForConfirmation(acceptDelete);
        confirmation.Title.ShouldBe("メモの削除");
        confirmation.Ok.EmulateClick();
        confirmation.WaitForClosed(acceptDelete);
        if (changeVersion)
        {
            UiWait.Until(() => !string.IsNullOrEmpty(list.Error.Text), "版競合のエラーが表示されません。");
        }
        else
        {
            UiWait.Until(() => list.Notes.ItemCount == 1, "削除したメモが一覧に残っています。");
        }

        // 受け入れ条件
        var remaining = new SavedNote("保持するメモ", "保持する本文", 1);
        notesAfterCancel.ShouldBe(new[]
        {
            new SavedNote("削除するメモ", "削除対象の本文", 1),
            remaining,
        }, ignoreOrder: true);
        titlesAfterCancel.ShouldBe(new[] { "削除するメモ", "保持するメモ" }, ignoreOrder: true);
        targetIdAfterCancel.ShouldBe(Guid.Parse("b90b0486-a6bf-49e8-bf87-b90c0d18d3ac"));
        remainingIdAfterCancel.ShouldBe(Guid.Parse("d3115922-5803-4926-b902-d9c9572d8bc0"));
        app.Database.ReadNoteId("保持するメモ").ShouldBe(remainingIdAfterCancel);
        if (changeVersion)
        {
            list.Error.Text.ShouldBe("対象のメモは更新または削除されています。一覧を読み直してください。");
            list.Titles.ShouldBe(titlesAfterCancel, ignoreOrder: true);
            app.Database.ReadNotes().ShouldBe(new[]
            {
                new SavedNote("削除するメモ", "削除対象の本文", 2),
                remaining,
            }, ignoreOrder: true);
            app.Database.ReadNoteId("削除するメモ").ShouldBe(targetIdAfterCancel);
        }
        else
        {
            list.Titles.ShouldBe(new[] { "保持するメモ" });
            app.Database.ReadNotes().ShouldBe(new[] { remaining });
        }
        app.Capture(changeVersion ? "delete-version-conflict" : "note-deleted");
        app.Close();
    }
}
