using System;

namespace WpfNotesSample.Domain.Notes;

public sealed class NoteConflictException : Exception
{
    public NoteConflictException() : base("対象のノートは更新または削除されています。一覧を読み直してください。")
    {
    }

    public NoteConflictException(string message) : base(message)
    {
    }

    public NoteConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
