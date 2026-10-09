using System.Globalization;

namespace WpfNotesSample.Domain.Notes;

public static class NoteRules
{
    private const int MaxTitleLength = 100;
    private const int MaxBodyLength = 10_000;

    public static NoteInput Normalize(NoteInput input)
    {
        var title = input.Title?.Trim();
        if (title is null || new StringInfo(title).LengthInTextElements is < 1 or > MaxTitleLength)
        {
            throw new NoteValidationException("タイトルは1～100文字で入力してください。");
        }

        if (input.Body is null || new StringInfo(input.Body).LengthInTextElements > MaxBodyLength)
        {
            throw new NoteValidationException("本文は10,000文字以内で入力してください。");
        }

        return new NoteInput(title, input.Body);
    }
}
