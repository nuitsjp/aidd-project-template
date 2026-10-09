using System.Linq;
using Shouldly;
using WpfNotesSample.Domain.Notes;
using Xunit;

namespace WpfNotesSample.UnitTests;

public sealed class NoteRulesTests
{
    [Fact]
    public void TitleCountsSurrogatePairsAndCombiningCharactersAsTextElements()
    {
        var title = string.Concat(Enumerable.Repeat("\U0001F427e\u0301", 50));

        var normalized = NoteRules.Normalize(new NoteInput("  " + title + "  ", "本文"));

        normalized.Title.ShouldBe(title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t\r\n")]
    public void EmptyTrimmedTitleIsRejected(string title)
    {
        Should.Throw<NoteValidationException>(() => NoteRules.Normalize(new NoteInput(title, "")))
            .Message.ShouldBe("タイトルは1～100文字で入力してください。");
    }

    [Fact]
    public void TitleOverOneHundredTextElementsIsRejected()
    {
        var title = string.Concat(Enumerable.Repeat("\U0001F427", 101));

        Should.Throw<NoteValidationException>(() => NoteRules.Normalize(new NoteInput(title, "")));
    }

    [Fact]
    public void BodyAllowsTenThousandTextElementsAndPreservesWhitespace()
    {
        var body = " " + string.Concat(Enumerable.Repeat("e\u0301", 9_998)) + "\n";

        NoteRules.Normalize(new NoteInput("タイトル", body)).Body.ShouldBe(body);
    }

    [Fact]
    public void BodyOverTenThousandTextElementsIsRejected()
    {
        var body = string.Concat(Enumerable.Repeat("\U0001F427", 10_001));

        Should.Throw<NoteValidationException>(() => NoteRules.Normalize(new NoteInput("タイトル", body)))
            .Message.ShouldBe("本文は10,000文字以内で入力してください。");
    }
}
