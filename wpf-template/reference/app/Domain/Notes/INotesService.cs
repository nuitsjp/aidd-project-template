using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfNotesSample.Domain.Notes;

public interface INotesService
{
    Task<IReadOnlyList<Note>> ListAsync();
    Task<Note?> GetAsync(Guid id);
    Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body);
    Task RemoveAsync(Guid id, int expectedVersion);
}
