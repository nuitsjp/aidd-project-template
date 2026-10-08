using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfNotesSample.Model.Domain.Notes;

public interface INotesService
{
    Task<IReadOnlyList<Note>> ListAsync();
    Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body);
    Task RemoveAsync(Guid id, int expectedVersion);
    Task<IReadOnlyList<Note>> ImportAsync(IReadOnlyList<NoteInput> inputs);
}
