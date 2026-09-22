namespace Aidd.ReactDotnet.Domain;

internal sealed class AppFaultException : Exception
{
    internal AppFaultException(
        string code,
        string message,
        IReadOnlyDictionary<string, string>? fieldErrors = null)
        : base(message)
    {
        Code = code;
        FieldErrors = fieldErrors;
    }

    internal string Code { get; }

    internal IReadOnlyDictionary<string, string>? FieldErrors { get; }

    internal static AppFaultException Validation(
        string message = "入力の形式を確認してください。",
        IReadOnlyDictionary<string, string>? fieldErrors = null) =>
        new("VALIDATION", message, fieldErrors);
}
