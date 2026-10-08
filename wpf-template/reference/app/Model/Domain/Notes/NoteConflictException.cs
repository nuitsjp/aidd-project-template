using System;

namespace WpfNotesSample.Model.Domain.Notes;

public sealed class NoteConflictException : Exception
{
    public NoteConflictException() : base("対象のメモは更新または削除されています。一覧を読み直してください。")
    {
    }
}
