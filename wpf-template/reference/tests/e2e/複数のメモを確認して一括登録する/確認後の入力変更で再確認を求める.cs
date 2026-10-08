using Shouldly;
using WpfNotesSample.E2eTests.Support;
using Xunit;

namespace WpfNotesSample.E2eTests.UseCases.ImportNotes;

public sealed class ReconfirmChangedInputTests
{
    private readonly ITestOutputHelper output;

    public ReconfirmChangedInputTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ChangedInputInvalidatesThePreviewAndOnlyReconfirmedValuesAreSaved()
    {
        // 分岐条件
        using var app = new AppSession(output);
        app.Main.ShowImport.EmulateClick();
        var import = app.Main.ImportNotes;
        import.Input.EmulateChangeText("一件目\t本文一\n二件目\t本文二");
        import.Preview.EmulateClick();
        UiWait.Until(() => import.PreviewItems.ItemCount == 2 && import.Confirm.IsEnabled,
            "入力変更前の確認内容が表示されません。");
        import.PreviewItems.GetCellText(0, 0).ShouldBe("一件目");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("本文一");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("二件目");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("本文二");
        app.Database.ReadNotes().ShouldBeEmpty();

        // 手順1
        const string changedInput = "変更した一件目\t変更した本文\n二件目\t本文二";
        import.Input.EmulateChangeText(changedInput);
        UiWait.Until(() => !import.Confirm.IsEnabled && import.PreviewItems.ItemCount == 0,
            "入力変更後も古い確認内容が有効です。");
        import.Input.Text.Replace("\r\n", "\n").ShouldBe(changedInput);
        app.Database.ReadNotes().ShouldBeEmpty();

        // 手順2
        import.Preview.EmulateClick();
        UiWait.Until(() => import.PreviewItems.ItemCount == 2 && import.Confirm.IsEnabled,
            "変更後の確認内容が表示されません。");
        import.PreviewItems.GetCellText(0, 0).ShouldBe("変更した一件目");
        import.PreviewItems.GetCellText(0, 1).ShouldBe("変更した本文");
        import.PreviewItems.GetCellText(1, 0).ShouldBe("二件目");
        import.PreviewItems.GetCellText(1, 1).ShouldBe("本文二");
        import.Error.Text.ShouldBeEmpty();
        app.Database.ReadNotes().ShouldBeEmpty();

        // 受け入れ条件
        import.Confirm.EmulateClick();
        UiWait.Until(() => import.Status.Text == "2 件を登録しました。", "再確認したメモの登録が完了しません。");
        import.Input.Text.ShouldBeEmpty();
        app.Database.ReadNotes().ShouldBe(new[]
        {
            new SavedNote("変更した一件目", "変更した本文", 1),
            new SavedNote("二件目", "本文二", 1),
        }, ignoreOrder: true);
        UiWait.Until(() => app.Main.ShowNotes.IsEnabled, "再確認したメモの登録後に一覧へ移動できません。");
        app.Main.ShowNotes.EmulateClick();
        var list = app.Main.NoteList;
        UiWait.Until(() => list.Notes.ItemCount == 2, "再確認して登録したメモが一覧に表示されません。");
        list.Titles.ShouldBe(new[] { "変更した一件目", "二件目" }, ignoreOrder: true);
        app.Close();
    }
}
