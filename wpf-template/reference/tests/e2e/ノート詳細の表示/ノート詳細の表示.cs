using System;
using System.Linq;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.ShowNote;

public sealed class ShowNoteScenarioTests
{
    private readonly ITestOutputHelper _output;
    public ShowNoteScenarioTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("latest")]
    [InlineData("missing")]
    [InlineData("read-error")]
    public void DetailsFetchTheLatestSelectedNoteOrShowTheFailure(string mode)
    {
        // 開始条件: 保存済みノートを一覧に表示し、その後のDB変更を用意する。
        using var app = new AppSession(_output);
        app.Database.ExecuteSql("""
            INSERT INTO notes VALUES ('11111111-1111-1111-1111-111111111111',
                '一覧取得時のタイトル', '一覧取得時の本文', 1, '2026-01-01T00:00:00.0000000Z');
            """);
        var list = app.Main.NoteList;
        list.Refresh.EmulateClick();
        UiWait.Until(() => list.Notes.ItemCount == 1 && list.Refresh.IsEnabled, "保存済みノートが表示されません。");
        if (mode == "latest")
        {
            app.Database.ExecuteSql("UPDATE notes SET title = '最新のタイトル', body = '最新の本文', version = 2;");
        }

        if (mode == "missing")
        {
            app.Database.ExecuteSql("DELETE FROM notes;");
        }

        var notesBeforeDetails = app.Database.ReadNotes();
        var expected = mode == "latest"
            ? new SavedNote("最新のタイトル", "最新の本文", 2)
            : new SavedNote("一覧取得時のタイトル", "一覧取得時の本文", 1);
        if (mode == "missing")
        {
            notesBeforeDetails.ShouldBeEmpty();
        }
        else
        {
            notesBeforeDetails.Single().ShouldBe(expected);
            app.Database.ReadNoteId(expected.Title).ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }
        if (mode == "read-error")
        {
            app.Database.ExecuteSql("ALTER TABLE notes RENAME TO saved_notes;");
        }

        // 手順1: 一覧の行で選んだノートの最新の詳細を取得して表示する。
        list.ShowDetails(0).EmulateClick();
        var details = app.Main.NoteDetails;
        UiWait.Until(() => details.Back.IsEnabled && (mode == "latest"
            ? details.Title.Text == "最新のタイトル" : !string.IsNullOrEmpty(details.Error.Text)),
            "詳細の取得が完了しません。");
        if (mode == "latest")
        {
            details.Title.Text.ShouldBe("最新のタイトル");
            details.Body.Text.ShouldBe("最新の本文");
            details.Error.Text.ShouldBeEmpty();
            details.Edit.IsEnabled.ShouldBeTrue();
            details.Delete.IsEnabled.ShouldBeTrue();
            app.Capture("note-details");
        }
        else
        {
            details.Error.Text.ShouldNotBeEmpty();
            if (mode == "missing")
            {
                details.Error.Text.ShouldBe("対象のノートは削除されています。一覧を読み直してください。");
            }

            if (mode == "read-error")
            {
                details.Error.Text.ShouldContain("no such table: notes");
            }

            details.Title.Text.ShouldBeEmpty();
            details.Body.Text.ShouldBeEmpty();
            details.Edit.IsEnabled.ShouldBeFalse();
            details.Delete.IsEnabled.ShouldBeFalse();
        }

        // 手順2: 詳細画面から一覧へ戻る。
        if (mode == "read-error")
        {
            app.Database.ExecuteSql("ALTER TABLE saved_notes RENAME TO notes;");
        }

        details.Back.EmulateClick();
        list = app.Main.NoteList;
        UiWait.Until(() => list.Refresh.IsEnabled && list.Notes.ItemCount == (mode == "missing" ? 0 : 1),
            "詳細画面から一覧へ戻れません。");

        // 受け入れ条件: 最新値を表示し、対象なし・取得失敗では編集を許可せずDBを変更しない。
        list.Error.Text.ShouldBeEmpty();
        if (mode == "latest")
        {
            list.Titles.ShouldBe(new[] { "最新のタイトル" });
        }

        app.Database.ReadNotes().ShouldBe(notesBeforeDetails);
        if (mode != "missing")
        {
            app.Database.ReadNotes().Single().ShouldBe(expected);
            app.Database.ReadNoteId(expected.Title).ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }
        app.Close();
    }
}
