using System;
using System.Linq;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.UpdateNote;

public sealed class UpdateNoteScenarioTests
{
    private readonly ITestOutputHelper _output;
    public UpdateNoteScenarioTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("success")]
    [InlineData("list-error")]
    [InlineData("invalid")]
    [InlineData("write-error")]
    [InlineData("locked")]
    [InlineData("conflict")]
    [InlineData("deleted")]
    public void UpdateProtectsTheDraftAndCommittedValuesAtEachFailureBoundary(string mode)
    {
        // 開始条件: 保存済みノートの詳細画面を表示する。
        using var app = new AppSession(_output);
        app.Database.ExecuteSql("""
            INSERT INTO notes VALUES ('11111111-1111-1111-1111-111111111111',
                '保存済みタイトル', '保存済み本文', 1, '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && list.Refresh.IsEnabled, "更新対象のノートが表示されません。");
        list.ShowDetails(0).EmulateClick();
        var details = app.Main.NoteDetails;
        UiWait.Until(() => details.Edit.IsEnabled, "保存済みノートを更新できません。");

        // 手順1: 詳細画面で編集を選び、現在の値を編集画面に表示する。
        details.Edit.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.Text.ShouldBe("保存済みタイトル");
        editor.Body.Text.ShouldBe("保存済み本文");

        // 手順2: タイトルと本文を変更する。
        var inputTitle = mode == "invalid" ? new string('題', 101) : "  更新したノート　";
        const string body = "更新した本文\r\n改行後の本文";
        editor.Title.EmulateChangeText(inputTitle);
        editor.Body.EmulateChangeText(body);
        if (mode == "write-error")
        {
            app.Database.ExecuteSql("""
                CREATE TRIGGER fail_save BEFORE UPDATE ON notes
                BEGIN SELECT RAISE(ABORT, 'forced save failure'); END;
                """);
        }

        if (mode == "conflict")
        {
            app.Database.ExecuteSql("UPDATE notes SET title = '他の操作による更新', body = '新しい版の本文', version = 2;");
        }

        if (mode == "deleted")
        {
            app.Database.ExecuteSql("DELETE FROM notes;");
        }

        using var writeLock = mode == "locked" ? app.Database.HoldWriteLock() : null;
        var success = mode == "success" || mode == "list-error";

        // 手順3: 版を照合して保存し、入力・DB・版競合の失敗では下書きを保持する。
        editor.Save.EmulateClick();
        if (mode == "locked")
        {
            UiWait.Until(() => !editor.Save.IsEnabled, "保存中の重複実行が無効になりません。");
            editor.Back.IsEnabled.ShouldBeFalse();
            editor.Discard.IsEnabled.ShouldBeFalse();
            var busyClose = new Async();
            app.Main.Core.Close(busyClose);
            UiWait.Until(() => busyClose.IsCompleted, "保存中の終了操作が完了しません。");
            busyClose.WaitForCompletion();
            app.Main.Core.IsWindow().ShouldBeTrue();
        }
        UiWait.Until(() => success ? editor.Status.Text == "保存しました。" : !string.IsNullOrEmpty(editor.Error.Text),
            "更新の保存結果が表示されません。");
        UiWait.Until(() => editor.Back.IsEnabled, "更新後の操作が有効になりません。");
        if (success)
        {
            editor.Title.Text.ShouldBe("更新したノート");
            editor.Body.Text.ShouldBe(body);
            editor.Error.Text.ShouldBeEmpty();
            if (mode == "success")
            {
                app.Capture("note-update");
            }
        }
        else
        {
            if (mode == "invalid")
            {
                editor.Error.Text.ShouldBe("タイトルは1～100文字で入力してください。");
            }

            if (mode == "write-error")
            {
                editor.Error.Text.ShouldContain("forced save failure");
            }

            if (mode == "locked")
            {
                editor.Error.Text.ShouldContain("database is locked");
            }

            if (mode == "conflict" || mode == "deleted")
            {
                editor.Error.Text.ShouldBe("対象のノートは更新または削除されています。一覧を読み直してください。");
            }

            editor.Title.Text.ShouldBe(inputTitle);
            editor.Body.Text.ShouldBe(body);
            editor.Status.Text.ShouldBeEmpty();
        }

        // 手順4: 成功後は一覧へ戻り、未保存の場合は破棄確認を取り消して編集を続ける。
        if (success)
        {
            if (mode == "list-error")
            {
                app.Database.ExecuteSql("ALTER TABLE notes RENAME TO saved_notes;");
            }

            editor.Back.EmulateClick();
            list = app.Main.NoteList;
            if (mode == "list-error")
            {
                UiWait.Until(() => !string.IsNullOrEmpty(list.Error.Text), "保存後の一覧取得失敗が表示されません。");
                list.Error.Text.ShouldContain("no such table: notes");
                editor.Status.Text.ShouldBe("保存しました。");
                editor.Error.Text.ShouldBeEmpty();
                app.Database.ExecuteSql("ALTER TABLE saved_notes RENAME TO notes;");
                UiWait.Until(() => list.Refresh.IsEnabled, "失敗後に一覧を再取得できません。");
                list.Refresh.EmulateClick();
            }
            UiWait.Until(() => list.Notes.ItemCount == 1 && list.Titles[0] == "更新したノート", "更新したノートが一覧に表示されません。");
        }
        else
        {
            var leave = new Async();
            editor.Back.EmulateClick(leave);
            var confirmation = app.Main.WaitForConfirmation(leave);
            confirmation.Title.ShouldBe("変更の破棄");
            confirmation.Cancel.EmulateClick();
            confirmation.WaitForClosed(leave);
            app.Main.NoteEdit.Title.Text.ShouldBe(inputTitle);
            app.Main.NoteEdit.Body.Text.ShouldBe(body);
        }

        // 受け入れ条件: 保存確定値と版を維持し、失敗や離脱取消で新しいDB値・下書きを失わない。
        if (success)
        {
            var expected = new SavedNote("更新したノート", body, 2);
            app.Database.ReadNotes().Single().ShouldBe(expected);
            app.Database.ReadNoteId("更新したノート").ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            app.Close();
            app.Start();
            list = app.Main.NoteList;
            UiWait.Until(() => list.Notes.ItemCount == 1, "再起動後の更新したノートが表示されません。");
            app.Database.ReadNotes().Single().ShouldBe(expected);
            list.Titles.ShouldBe(new[] { "更新したノート" });
        }
        else
        {
            if (mode == "deleted")
            {
                app.Database.ReadNotes().ShouldBeEmpty();
            }
            else
            {
                app.Database.ReadNotes().Single().ShouldBe(mode == "conflict"
                ? new SavedNote("他の操作による更新", "新しい版の本文", 2)
                : new SavedNote("保存済みタイトル", "保存済み本文", 1));
            }

            var exit = new Async();
            app.Main.Core.Close(exit);
            var confirmation = app.Main.WaitForConfirmation(exit);
            confirmation.Cancel.EmulateClick();
            confirmation.WaitForClosed(exit);
            app.Main.NoteEdit.Title.Text.ShouldBe(inputTitle);
            app.Main.NoteEdit.Body.Text.ShouldBe(body);
            editor.Discard.EmulateClick();
        }
        app.Close();
    }
}
