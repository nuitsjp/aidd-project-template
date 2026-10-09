using System;
using System.Linq;
using Codeer.Friendly;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.AddNote;

public sealed class AddNoteScenarioTests
{
    private readonly ITestOutputHelper _output;
    public AddNoteScenarioTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("success")]
    [InlineData("invalid")]
    [InlineData("write-error")]
    public void AddPersistsNormalizedValuesOrKeepsTheUnsavedDraft(string mode)
    {
        // 開始条件: 空のノート一覧から新しいノートを追加する。
        using var app = new AppSession(_output);
        var list = app.Main.NoteList;
        list.Notes.ItemCount.ShouldBe(0);

        // 手順1: 新規追加を選ぶと空の編集画面が表示される。
        list.NewNote.EmulateClick();
        var editor = app.Main.NoteEdit;
        editor.Title.Text.ShouldBeEmpty();
        editor.Body.Text.ShouldBeEmpty();

        // 手順2: タイトルと本文を入力する。
        var inputTitle = mode == "invalid" ? new string('題', 101) : "  追加したノート　";
        const string body = "追加する本文\r\n改行後の本文";
        editor.Title.EmulateChangeText(inputTitle);
        editor.Body.EmulateChangeText(body);
        if (mode == "write-error")
        {
            app.Database.ExecuteSql("""
                CREATE TRIGGER fail_save BEFORE INSERT ON notes
                BEGIN SELECT RAISE(ABORT, 'forced save failure'); END;
                """);
        }

        // 手順3: 保存し、成功した値を確定する。入力・書き込み失敗では下書きを保持する。
        editor.Save.EmulateClick();
        UiWait.Until(() => mode == "success" ? editor.Status.Text == "保存しました。" : !string.IsNullOrEmpty(editor.Error.Text),
            "ノートの保存結果が表示されません。");
        UiWait.Until(() => editor.Back.IsEnabled, "保存後の操作が有効になりません。");
        if (mode == "success")
        {
            editor.Title.Text.ShouldBe("追加したノート");
            editor.Body.Text.ShouldBe(body);
            editor.Error.Text.ShouldBeEmpty();
            app.Capture("note-add");
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

            editor.Title.Text.ShouldBe(inputTitle);
            editor.Body.Text.ShouldBe(body);
            editor.Status.Text.ShouldBeEmpty();
            app.Database.ReadNotes().ShouldBeEmpty();
        }

        // 手順4: 保存成功後は一覧へ戻り、未保存の離脱確認を取り消した場合は下書きを維持する。
        if (mode == "success")
        {
            editor.Back.EmulateClick();
            list = app.Main.NoteList;
            UiWait.Until(() => list.Notes.ItemCount == 1, "追加したノートが一覧に表示されません。");
        }
        else
        {
            var leave = new Async();
            editor.Back.EmulateClick(leave);
            var confirmation = app.Main.WaitForConfirmation(leave);
            confirmation.Title.ShouldBe("変更の破棄");
            confirmation.Message.ShouldBe("保存していない変更を破棄しますか？");
            confirmation.Cancel.EmulateClick();
            confirmation.WaitForClosed(leave);
            app.Main.NoteEdit.Title.Text.ShouldBe(inputTitle);
            app.Main.NoteEdit.Body.Text.ShouldBe(body);
        }

        // 受け入れ条件: 正規化した値・新規ID・版を再起動後も保持し、失敗時はDBを変更しない。
        if (mode == "success")
        {
            list.Titles.ShouldBe(new[] { "追加したノート" });
            app.Database.ReadNotes().Single().ShouldBe(new SavedNote("追加したノート", body, 1));
            var savedId = app.Database.ReadNoteId("追加したノート");
            savedId.ShouldNotBe(Guid.Empty);
            app.Close();
            app.Start();
            list = app.Main.NoteList;
            UiWait.Until(() => list.Notes.ItemCount == 1, "再起動後のノートが表示されません。");
            app.Database.ReadNoteId("追加したノート").ShouldBe(savedId);
            list.ShowDetails(0).EmulateClick();
            var details = app.Main.NoteDetails;
            UiWait.Until(() => details.Back.IsEnabled && details.Title.Text == "追加したノート", "再起動後の詳細が表示されません。");
            details.Title.Text.ShouldBe("追加したノート");
            details.Body.Text.ShouldBe(body);
        }
        else
        {
            var exit = new Async();
            app.Main.Core.Close(exit);
            var confirmation = app.Main.WaitForConfirmation(exit);
            confirmation.Cancel.EmulateClick();
            confirmation.WaitForClosed(exit);
            app.Main.NoteEdit.Title.Text.ShouldBe(inputTitle);
            app.Main.NoteEdit.Body.Text.ShouldBe(body);
            app.Database.ReadNotes().ShouldBeEmpty();
            editor.Discard.EmulateClick();
        }
        app.Close();
    }
}
