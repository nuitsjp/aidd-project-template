using NotesSample.Domain;
using System.Text;

namespace NotesSample.Domain.Notes;

internal static class NoteRules
{
    internal static void ValidateTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.EnumerateRunes().Count() > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100文字で入力してください。");
        }
    }

    internal static void ValidateBody(string value)
    {
        if (!IsValidBody(value))
        {
            throw AppFaultException.Validation("本文は10,000文字以内で入力してください。");
        }
    }

    internal static bool IsValidBody(string? value) =>
        value is not null && value.EnumerateRunes().Count() <= 10_000;

}
