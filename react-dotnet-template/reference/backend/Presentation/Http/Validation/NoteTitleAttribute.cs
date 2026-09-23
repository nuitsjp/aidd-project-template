using System.ComponentModel.DataAnnotations;

namespace NotesSample.Presentation.Http.Validation;

internal sealed class NoteTitleAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string title && !string.IsNullOrWhiteSpace(title) && title.EnumerateRunes().Count() <= 100;

    public override string FormatErrorMessage(string name) => "タイトルは1〜100文字で入力してください。";
}
