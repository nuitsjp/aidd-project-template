using System.ComponentModel.DataAnnotations;

namespace NotesSample.Presentation.Http.Validation;

internal sealed class RuneMaxLengthAttribute(int maximum) : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && text.EnumerateRunes().Count() <= maximum;
}
