using System;

namespace WpfNotesSample.Model.Domain.Notes;

public sealed class NoteValidationException : Exception
{
    public NoteValidationException(string message) : base(message)
    {
    }
}
