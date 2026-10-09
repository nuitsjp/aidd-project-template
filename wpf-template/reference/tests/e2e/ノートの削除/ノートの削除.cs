using System;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.DeleteNote;

public sealed class DeleteNoteScenarioTests
{
    private readonly ITestOutputHelper _output;
    public DeleteNoteScenarioTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("success")]
    [InlineData("conflict")]
    [InlineData("write-error")]
    public void CancellationKeepsAllNotesAndApprovalDeletesOnlyTheDisplayedVersion(string mode)
    {
        // 開始条件: 削除対象と保持するノートを用意し、削除対象の詳細を表示する。
        using var app = new AppSession(_output);
        app.Database.ExecuteSql("""
            INSERT INTO notes VALUES
                ('11111111-1111-1111-1111-111111111111', '削除するノート', '削除対象の本文', 1, '2026-01-02T00:00:00.0000000Z'),
                ('22222222-2222-2222-2222-222222222222', '保持するノート', '保持する本文', 1, '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 2 && list.Refresh.IsEnabled, "削除対象のノートが表示されません。");
        list.ShowDetails(Array.IndexOf(list.Titles, "削除するノート")).EmulateClick();
        var details = app.Main.NoteDetails;
        UiWait.Until(() => details.Delete.IsEnabled && details.Title.Text == "削除するノート", "削除対象の詳細が表示されません。");

        // 手順1: 削除を選ぶと対象のタイトルを示す確認を表示する。
        var cancel = new Async();
        details.Delete.EmulateClick(cancel);
        var confirmation = app.Main.WaitForConfirmation(cancel);
        confirmation.Title.ShouldBe("ノートの削除");
        confirmation.Message.ShouldBe("「削除するノート」を削除しますか？");

        // 手順2: 確認を取り消すと詳細の表示とDBを変更しない。
        confirmation.Cancel.EmulateClick();
        confirmation.WaitForClosed(cancel);
        var remaining = new SavedNote("保持するノート", "保持する本文", 1);
        app.Database.ReadNotes().ShouldBe(new[] { new SavedNote("削除するノート", "削除対象の本文", 1), remaining }, ignoreOrder: true);
        details.Title.Text.ShouldBe("削除するノート");
        details.Body.Text.ShouldBe("削除対象の本文");
        if (mode == "conflict")
        {
            app.Database.ExecuteSql("UPDATE notes SET version = 2 WHERE id = '11111111-1111-1111-1111-111111111111';");
        }

        if (mode == "write-error")
        {
            app.Database.ExecuteSql("""
                CREATE TRIGGER fail_delete BEFORE DELETE ON notes
                BEGIN SELECT RAISE(ABORT, 'forced delete failure'); END;
                """);
        }

        // 手順3: 再び削除を選び承認すると版を照合して削除し、一覧へ戻る。
        var accept = new Async();
        details.Delete.EmulateClick(accept);
        confirmation = app.Main.WaitForConfirmation(accept);
        confirmation.Title.ShouldBe("ノートの削除");
        confirmation.Ok.EmulateClick();
        confirmation.WaitForClosed(accept);
        if (mode == "success")
        {
            list = app.Main.NoteList;
            UiWait.Until(() => list.Refresh.IsEnabled && list.Notes.ItemCount == 1, "削除後の一覧が表示されません。");
        }
        else
        {
            UiWait.Until(() => !string.IsNullOrEmpty(details.Error.Text) && details.Delete.IsEnabled, "削除失敗が表示されません。");
        }

        // 受け入れ条件: 対象だけを削除し、取消・版競合・DB失敗では既存の値と詳細の表示を維持する。
        app.Database.ReadNoteId("保持するノート").ShouldBe(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        if (mode == "success")
        {
            list.Titles.ShouldBe(new[] { "保持するノート" });
            app.Database.ReadNotes().ShouldBe(new[] { remaining });
            app.Capture("note-delete");
            app.Close();
            app.Start();
            list = app.Main.NoteList;
            UiWait.Until(() => list.Notes.ItemCount == 1, "再起動後の削除結果が表示されません。");
            list.Titles.ShouldBe(new[] { "保持するノート" });
        }
        else
        {
            if (mode == "conflict")
            {
                details.Error.Text.ShouldBe("対象のノートは更新または削除されています。一覧を読み直してください。");
            }
            else
            {
                details.Error.Text.ShouldContain("forced delete failure");
            }

            details.Title.Text.ShouldBe("削除するノート");
            details.Body.Text.ShouldBe("削除対象の本文");
            app.Database.ReadNotes().ShouldBe(new[]
            {
                new SavedNote("削除するノート", "削除対象の本文", mode == "conflict" ? 2 : 1),
                remaining,
            }, ignoreOrder: true);
            app.Database.ReadNoteId("削除するノート").ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }
        app.Close();
    }
}
