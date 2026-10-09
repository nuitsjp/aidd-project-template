using System;

namespace WpfNotesSample.Domain.Notes;

public sealed class NoteValidationException : Exception
{
    public NoteValidationException() : base("入力内容を確認してください。")
    {
    }

    public NoteValidationException(string message) : base(message)
    {
    }

    public NoteValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
