using NotesSample.Domain;
using NotesSample.Domain.Notes;
using Shouldly;
using Xunit;

namespace NotesSample.UnitTests;

public sealed class NoteRulesTests
{
    public sealed class ValidateTitle
    {
        [Fact]
        public void OneHundredUnicodeCharacters_AreAccepted()
        {
            NoteRules.ValidateTitle(new string('あ', 100));
            NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 100)));
        }

        [Fact]
        public void OneHundredAndOneUnicodeCharacters_ThrowValidationFault()
        {
            var error = Should.Throw<AppFaultException>(() =>
                NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 101))));

            error.Code.ShouldBe("VALIDATION");
        }

        [Fact]
        public void WhitespaceOnly_ThrowsValidationFault()
        {
            var error = Should.Throw<AppFaultException>(() => NoteRules.ValidateTitle("  "));

            error.Code.ShouldBe("VALIDATION");
        }
    }

    public sealed class ValidateBody
    {
        [Fact]
        public void TenThousandUnicodeCharacters_AreAccepted()
        {
            NoteRules.ValidateBody(string.Concat(Enumerable.Repeat("😀", 10_000)));
        }

        [Fact]
        public void TenThousandAndOneUnicodeCharacters_ThrowValidationFault()
        {
            var error = Should.Throw<AppFaultException>(() =>
                NoteRules.ValidateBody(string.Concat(Enumerable.Repeat("😀", 10_001))));

            error.Code.ShouldBe("VALIDATION");
        }
    }

    public sealed class IsValidBody
    {
        [Fact]
        public void Null_ReturnsFalse()
        {
            NoteRules.IsValidBody(null).ShouldBeFalse();
        }
    }
}
