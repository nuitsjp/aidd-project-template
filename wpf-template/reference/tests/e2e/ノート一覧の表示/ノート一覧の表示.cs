using System;
using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.ListNotes;

public sealed class ListNotesScenarioTests
{
    private readonly ITestOutputHelper _output;
    public ListNotesScenarioTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("empty")]
    [InlineData("refresh")]
    [InlineData("read-error")]
    public void StartupAndRefreshShowPersistedNotesWithoutLosingTheListOnFailure(string mode)
    {
        // 開始条件: 保存済みのノートが存在する場合と空の場合を用意する。
        using var app = new AppSession(_output);
        if (mode != "empty")
        {
            app.Database.ExecuteSql("""
                INSERT INTO notes VALUES
                    ('11111111-1111-1111-1111-111111111111', '古いノート', '一覧に表示しない本文', 1, '2026-01-01T00:00:00.0000000Z'),
                    ('22222222-2222-2222-2222-222222222222', '新しいノート', '新しい本文', 1, '2026-01-02T00:00:00.0000000Z');
                """);
        }

        app.Close();

        // 手順1: 起動時に保存済みのノート一覧を表示する。
        app.Start();
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == (mode == "empty" ? 0 : 2), "起動時の一覧が表示されません。");
        list.Titles.ShouldBe(mode == "empty" ? Array.Empty<string>() : new[] { "新しいノート", "古いノート" });
        list.Error.Text.ShouldBeEmpty();
        if (mode != "empty")
        {
            list.Notes.GetCellText(0, 1).ShouldBe("2026-01-02 00:00");
        }

        if (mode == "refresh")
        {
            app.Capture("note-list");
        }

        // 手順2: 再取得によって一覧を更新し、取得失敗では直前の一覧を保持する。
        if (mode == "refresh")
        {
            app.Database.ExecuteSql("UPDATE notes SET title = '再取得したノート', updated_at = '2026-01-03T00:00:00.0000000Z' WHERE id = '11111111-1111-1111-1111-111111111111';");
        }

        if (mode == "read-error")
        {
            app.Database.ExecuteSql("ALTER TABLE notes RENAME TO saved_notes;");
        }

        list.Refresh.EmulateClick();
        if (mode == "read-error")
        {
            UiWait.Until(() => !string.IsNullOrEmpty(list.Error.Text), "一覧の取得失敗が表示されません。");
            list.Error.Text.ShouldContain("no such table: notes");
        }
        else
        {
            UiWait.Until(() => list.Refresh.IsEnabled, "一覧の再取得が完了しません。");
            if (mode == "refresh")
            {
                UiWait.Until(() => list.Titles[0] == "再取得したノート", "DBの最新値が一覧に反映されません。");
            }
        }

        // 受け入れ条件: タイトルと更新日時を一覧に表示し、空と取得失敗を区別する。
        list.Titles.ShouldBe(mode switch
        {
            "empty" => Array.Empty<string>(),
            "refresh" => new[] { "再取得したノート", "新しいノート" },
            _ => new[] { "新しいノート", "古いノート" },
        });
        if (mode == "read-error")
        {
            app.Database.ExecuteSql("ALTER TABLE saved_notes RENAME TO notes;");
        }

        app.Database.ReadNotes().Count.ShouldBe(mode == "empty" ? 0 : 2);
        app.Close();
    }
}
