using Aidd.ReactDotnet.Domain;
using System.Text;

namespace Aidd.ReactDotnet.Domain.Notes;

internal static class NoteRules
{
    internal static void ValidateTitle(string value)
    {
        if (!IsValidTitle(value))
        {
            throw AppFaultException.Validation("タイトルは1〜100文字で入力してください。");
        }
    }

    internal static bool IsValidTitle(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.EnumerateRunes().Count() <= 100;

    internal static string NormalizeTitle(string value) => value.Trim();

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
