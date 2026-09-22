using Aidd.ReactDotnet.Domain;
using System.Text;

namespace Aidd.ReactDotnet.Domain.Notes;

internal static class NoteRules
{
    internal static string ValidateTitle(string value)
    {
        var title = value.Trim();
        if (title.Length == 0 || title.EnumerateRunes().Count() > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100文字で入力してください。",
                new Dictionary<string, string> { ["title"] = "1〜100文字で入力してください。" });
        }

        return title;
    }

    internal static void ValidateBody(string value)
    {
        if (value.EnumerateRunes().Count() > 10_000)
        {
            throw AppFaultException.Validation("本文は10,000文字以内で入力してください。",
                new Dictionary<string, string> { ["body"] = "10,000文字以内で入力してください。" });
        }
    }

}
