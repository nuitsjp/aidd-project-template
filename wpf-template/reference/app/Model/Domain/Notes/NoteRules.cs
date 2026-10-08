using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WpfNotesSample.Model.Domain.Notes;

public static class NoteRules
{
    public static NoteInput Normalize(NoteInput input)
    {
        var title = input.Title?.Trim();
        if (title is null || new StringInfo(title).LengthInTextElements is < 1 or > 100)
        {
            throw new NoteValidationException("タイトルは1～100文字で入力してください。");
        }

        if (input.Body is null || new StringInfo(input.Body).LengthInTextElements > 10_000)
        {
            throw new NoteValidationException("本文は10,000文字以内で入力してください。");
        }

        return new NoteInput(title, input.Body);
    }

    public static IReadOnlyList<NoteInput> NormalizeAll(IReadOnlyList<NoteInput> inputs)
    {
        if (inputs.Count is < 1 or > 100)
        {
            throw new NoteValidationException("一括登録は1～100件で入力してください。");
        }

        return inputs.Select(Normalize).ToList().AsReadOnly();
    }
}
