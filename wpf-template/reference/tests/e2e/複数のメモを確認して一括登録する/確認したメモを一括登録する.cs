using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.ImportNotes;

public sealed class ImportConfirmedNotesTests
{
    private readonly ITestOutputHelper output;

    public ImportConfirmedNotesTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ConfirmedNotesAreSavedTogether()
    {
        // 開始条件
        using var app = new AppSession(output);
        app.Main.ShowImport.EmulateClick();
        var import = app.Main.ImportNotes;
        import.Input.Text.ShouldBeEmpty();
        import.PreviewItems.ItemCount.ShouldBe(0);
        import.Confirm.IsEnabled.ShouldBeFalse();
        app.Database.ReadNotes().ShouldBeEmpty();

        // 手順1
        const string input = "  一件目  \t本文一\n二件目\t本文二";
        import.Input.EmulateChangeText(input);
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(input);
        import.PreviewItems.ItemCount.ShouldBe(0);
        import.Confirm.IsEnabled.ShouldBeFalse();
        app.Database.ReadNotes().ShouldBeEmpty();

        // 手順2
        import.Preview.EmulateClick();
        UiWait.Until(() => import.PreviewItems.ItemCount == 2 && import.Confirm.IsEnabled,
            "一括登録の確認内容が表示されません。");
        import.PreviewItems.GetCellText(0, 0).ShouldBe("一件目");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("本文一");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("二件目");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("本文二");
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(input);
        import.Error.Text.ShouldBeEmpty();
        app.Database.ReadNotes().ShouldBeEmpty();
        app.Capture("import-preview");

        // 手順3
        import.Confirm.EmulateClick();
        UiWait.Until(() => import.Status.Text == "2 件を登録しました。", "一括登録が完了しません。");
        import.Input.Text.ShouldBeEmpty();
        import.PreviewItems.ItemCount.ShouldBe(0);
        import.Confirm.IsEnabled.ShouldBeFalse();
        import.Error.Text.ShouldBeEmpty();

        // 受け入れ条件
        app.Database.ReadNotes().ShouldBe(new[]
        {
            new SavedNote("一件目", "本文一", 1),
            new SavedNote("二件目", "本文二", 1),
        }, ignoreOrder: true);
        UiWait.Until(() => app.Main.ShowNotes.IsEnabled, "一括登録後に一覧へ移動できません。");
        app.Main.ShowNotes.EmulateClick();
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "一括登録したメモが一覧に表示されません。");
        list.Titles.ShouldBe(new[] { "一件目", "二件目" }, ignoreOrder: true);
        app.Close();
    }
}
