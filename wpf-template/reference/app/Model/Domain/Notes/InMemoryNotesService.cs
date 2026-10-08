using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WpfNotesSample.Model.Domain.Notes;

public sealed class InMemoryNotesService : INotesService
{
    private readonly List<Note> notes = new List<Note>
    {
        new Note(Guid.Parse("d0152e10-076c-4c49-8fd7-b60e5a1e4d01"), "はじめてのメモ", "このデータはモック用です。保存内容はアプリの終了時に破棄されます。", 1, DateTime.UtcNow),
        new Note(Guid.Parse("d0152e10-076c-4c49-8fd7-b60e5a1e4d02"), "確認して一括登録", "一括登録の画面で複数のメモを確認してから保存できます。", 1, DateTime.UtcNow),
    };

    public Task<IReadOnlyList<Note>> ListAsync() => Task.FromResult<IReadOnlyList<Note>>(
        notes.OrderByDescending(note => note.UpdatedAt).ThenBy(note => note.Id).ToList().AsReadOnly());

    public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body)
    {
        var input = NoteRules.Normalize(new NoteInput(title, body));
        var current = id is null ? null : notes.SingleOrDefault(note => note.Id == id.Value);
        if (id is not null && (current is null || current.Version != expectedVersion))
        {
            throw new NoteConflictException();
        }

        var saved = new Note(id ?? Guid.NewGuid(), input.Title, input.Body, (current?.Version ?? 0) + 1, DateTime.UtcNow);
        if (current is not null)
        {
            notes.Remove(current);
        }

        notes.Add(saved);
        return Task.FromResult(saved);
    }

    public Task RemoveAsync(Guid id, int expectedVersion)
    {
        var current = notes.SingleOrDefault(note => note.Id == id);
        if (current is null || current.Version != expectedVersion)
        {
            throw new NoteConflictException();
        }

        notes.Remove(current);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Note>> ImportAsync(IReadOnlyList<NoteInput> inputs)
    {
        var prepared = NoteRules.NormalizeAll(inputs);
        IReadOnlyList<Note> imported = prepared.Select(input =>
            new Note(Guid.NewGuid(), input.Title, input.Body, 1, DateTime.UtcNow)).ToList().AsReadOnly();
        notes.AddRange(imported);
        return Task.FromResult(imported);
    }
}
