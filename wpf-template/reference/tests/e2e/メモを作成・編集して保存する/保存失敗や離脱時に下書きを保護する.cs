using System.Linq;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.EditNotes;

public sealed class ProtectDraftOnFailureAndLeaveTests
{
    private readonly ITestOutputHelper output;
    public ProtectDraftOnFailureAndLeaveTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData("invalid", false)]
    [InlineData("invalid", true)]
    [InlineData("write-error", false)]
    [InlineData("write-error", true)]
    [InlineData("locked", false)]
    [InlineData("locked", true)]
    public void FailedSaveAndCancelledLeavePreserveTheDraftAndSavedContent(string failure, bool exit)
    {
        // 分岐条件: 入力不正またはDB保存失敗があり、未保存の編集内容から離脱・終了する。
        using var app = new AppSession(output);
        app.Database.ExecuteSql("""
            INSERT INTO notes VALUES ('22222222-2222-2222-2222-222222222222',
                '保存済みタイトル', '保存済み本文', 1, '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && list.EditNote.IsEnabled, "保存済みメモが表示されません。");
        list.Notes.EmulateChangeCurrentCell(0, 0);
        list.EditNote.EmulateClick();
        var editor = app.Main.NoteEdit;
        var title = failure == "invalid" ? new string('題', 101) : "保存前の下書きタイトル";
        editor.Title.EmulateChangeText(title);
        editor.Body.EmulateChangeText("失敗しても保持する本文");
        if (failure == "write-error")
            app.Database.ExecuteSql("""
                CREATE TRIGGER fail_save BEFORE UPDATE ON notes
                BEGIN SELECT RAISE(ABORT, 'forced save failure'); END;
                """);
        using var writeLock = failure == "locked" ? app.Database.HoldWriteLock() : null;

        // 手順1: 保存失敗の原因を表示し、下書きと保存済みの内容を維持する。
        editor.Save.EmulateClick();
        if (failure == "locked")
        {
            UiWait.Until(() => !editor.Save.IsEnabled, "保存処理中の重複実行が無効になりません。");
            editor.Back.IsEnabled.ShouldBeFalse();
            editor.Discard.IsEnabled.ShouldBeFalse();
            app.Main.ShowNotes.IsEnabled.ShouldBeFalse();
            app.Main.ShowImport.IsEnabled.ShouldBeFalse();
            var busyClose = new Async();
            app.Main.Core.Close(busyClose);
            UiWait.Until(() => busyClose.IsCompleted, "保存処理中の終了操作が完了しません。");
            busyClose.WaitForCompletion();
            app.Main.Core.IsWindow().ShouldBeTrue();
        }
        UiWait.Until(() => !string.IsNullOrEmpty(editor.Error.Text), "保存失敗が表示されません。");
        if (failure == "write-error") editor.Error.Text.ShouldContain("forced save failure");
        if (failure == "locked") editor.Error.Text.ShouldContain("database is locked");
        editor.Title.Text.ShouldBe(title);
        editor.Body.Text.ShouldBe("失敗しても保持する本文");
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("保存済みタイトル", "保存済み本文", 1));

        // 手順2: 未保存のまま一覧への移動または終了を指示すると、破棄の確認を求める。
        UiWait.Until(() => editor.Back.IsEnabled, "保存失敗後に離脱操作を行えません。");
        var leave = new Async();
        if (exit) app.Main.Core.Close(leave);
        else editor.Back.EmulateClick(leave);
        var confirmation = app.Main.WaitForConfirmation(leave);
        confirmation.Title.ShouldBe("変更の破棄");
        confirmation.Message.ShouldBe("保存していない変更を破棄しますか？");

        // 手順3: 確認を取り消すと編集画面に留まり、入力内容を維持する。
        confirmation.Cancel.EmulateClick();
        confirmation.WaitForClosed(leave);
        app.Main.NoteEdit.Title.Text.ShouldBe(title);
        app.Main.NoteEdit.Body.Text.ShouldBe("失敗しても保持する本文");

        // 受け入れ条件: 入力・保存エラーと離脱取消で、下書きと保存済みの値を失わない。
        editor.Error.Text.ShouldNotBeEmpty();
        app.Database.ReadNotes().Single().ShouldBe(new SavedNote("保存済みタイトル", "保存済み本文", 1));
        app.Capture("save-failure-keeps-draft");
        editor.Discard.EmulateClick();
        app.Close();
    }
}
