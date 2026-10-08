using System.Linq;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.ImportNotes;

public sealed class RollBackFailedImportTests
{
    private readonly ITestOutputHelper output;

    public RollBackFailedImportTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void FailedImportRollsBackAllNewNotesAndKeepsTheInputAndPreview()
    {
        // 分岐条件
        using var app = new AppSession(output);
        app.Database.ExecuteSql("""
            INSERT INTO notes (id, title, body, version, updated_at)
            VALUES ('10000000-0000-0000-0000-000000000001', '既存のメモ', '保持する本文', 1, '2026-01-01T00:00:00.0000000Z');
            CREATE TRIGGER fail_second_import
            BEFORE INSERT ON notes WHEN NEW.title = '失敗させるメモ'
            BEGIN
                SELECT RAISE(ABORT, 'forced second insert failure');
            END;
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && list.Refresh.IsEnabled,
            "既存のメモが一覧に表示されません。");
        list.Notes.GetCellText(0, 0).ShouldBe("既存のメモ");
        list.Notes.GetCellText(0, 1).ShouldBe("保持する本文");
        app.Main.ShowImport.EmulateClick();
        var import = app.Main.ImportNotes;
        const string input = "先頭の正常なメモ\t追加されない本文\n失敗させるメモ\t失敗する本文";
        import.Input.EmulateChangeText(input);
        import.Preview.EmulateClick();
        UiWait.Until(() => import.PreviewItems.ItemCount == 2 && import.Confirm.IsEnabled,
            "保存失敗を検証する確認内容が表示されません。");
        import.PreviewItems.GetCellText(0, 0).ShouldBe("先頭の正常なメモ");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("追加されない本文");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("失敗させるメモ");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("失敗する本文");
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("既存のメモ", "保持する本文", 1));

        // 手順1
        import.Confirm.EmulateClick();
        UiWait.Until(() => !string.IsNullOrEmpty(import.Error.Text), "保存エラーが表示されません。");
        import.Error.Text.ShouldContain("forced second insert failure");
        import.Status.Text.ShouldBeEmpty();
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(input);
        import.PreviewItems.ItemCount.ShouldBe(2);
        import.PreviewItems.GetCellText(0, 0).ShouldBe("先頭の正常なメモ");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("追加されない本文");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("失敗させるメモ");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("失敗する本文");

        // 受け入れ条件
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("既存のメモ", "保持する本文", 1));
        UiWait.Until(() => import.Confirm.IsEnabled && app.Main.ShowNotes.IsEnabled,
            "失敗後の入力と確認内容を継続できません。");
        app.Capture("import-failure-draft");

        var cancelNavigation = new Async();
        app.Main.ShowNotes.EmulateClick(cancelNavigation);
        var confirmation = app.Main.WaitForConfirmation(cancelNavigation);
        confirmation.Title.ShouldBe("変更の破棄");
        confirmation.Cancel.EmulateClick();
        confirmation.WaitForClosed(cancelNavigation);
        import = app.Main.ImportNotes;
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(input);
        import.PreviewItems.ItemCount.ShouldBe(2);
        import.PreviewItems.GetCellText(0, 0).ShouldBe("先頭の正常なメモ");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("追加されない本文");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("失敗させるメモ");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("失敗する本文");

        var cancelExit = new Async();
        app.Main.Core.Close(cancelExit);
        confirmation = app.Main.WaitForConfirmation(cancelExit);
        confirmation.Title.ShouldBe("変更の破棄");
        confirmation.Cancel.EmulateClick();
        confirmation.WaitForClosed(cancelExit);
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(input);
        import.PreviewItems.ItemCount.ShouldBe(2);
        import.PreviewItems.GetCellText(0, 0).ShouldBe("先頭の正常なメモ");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("追加されない本文");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("失敗させるメモ");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("失敗する本文");
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("既存のメモ", "保持する本文", 1));

        // 片付けとして未保存の入力を破棄し、アプリを終了する。
        var discardImport = new Async();
        app.Main.ShowNotes.EmulateClick(discardImport);
        confirmation = app.Main.WaitForConfirmation(discardImport);
        confirmation.Ok.EmulateClick();
        confirmation.WaitForClosed(discardImport);
        list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 1, "保存失敗後に既存のメモが表示されません。");
        app.Close();
    }
}
