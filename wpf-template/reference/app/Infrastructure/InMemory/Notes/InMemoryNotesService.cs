using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfNotesSample.Domain.Notes;

namespace WpfNotesSample.Infrastructure.InMemory.Notes;

public sealed class InMemoryNotesService : INotesService
{
    private readonly List<Note> _notes = new List<Note>
    {
        new Note(Guid.Parse("d0152e10-076c-4c49-8fd7-b60e5a1e4d01"), "はじめてのノート", "このデータはモック用です。保存内容はアプリの終了時に破棄されます。", 1, DateTime.UtcNow),
        new Note(Guid.Parse("d0152e10-076c-4c49-8fd7-b60e5a1e4d02"), "ノートを編集する", "一覧からノートを開き、タイトルと本文を編集して保存できます。", 1, DateTime.UtcNow),
    };

    public Task<IReadOnlyList<Note>> ListAsync() => Task.FromResult<IReadOnlyList<Note>>(
        _notes.OrderByDescending(note => note.UpdatedAt).ThenBy(note => note.Id).ToList().AsReadOnly());

    public Task<Note?> GetAsync(Guid id) => Task.FromResult<Note?>(_notes.SingleOrDefault(note => note.Id == id));

    public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body)
    {
        var input = NoteRules.Normalize(new NoteInput(title, body));
        var current = id is null ? null : _notes.SingleOrDefault(note => note.Id == id.Value);
        if (id is not null && (current is null || current.Version != expectedVersion))
        {
            throw new NoteConflictException();
        }

        var saved = new Note(id ?? Guid.NewGuid(), input.Title, input.Body, (current?.Version ?? 0) + 1, DateTime.UtcNow);
        if (current is not null)
        {
            _notes.Remove(current);
        }

        _notes.Add(saved);
        return Task.FromResult(saved);
    }

    public Task RemoveAsync(Guid id, int expectedVersion)
    {
        var current = _notes.SingleOrDefault(note => note.Id == id);
        if (current is null || current.Version != expectedVersion)
        {
            throw new NoteConflictException();
        }

        _notes.Remove(current);
        return Task.CompletedTask;
    }
}
