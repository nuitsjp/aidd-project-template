using System.Collections.Generic;
using System.Threading.Tasks;
using WpfNotesSample.Model.Domain.Notes;

namespace WpfNotesSample.Model.UseCase;

public sealed class ConfirmedNotesImport
{
    private readonly INotesService notes;

    internal ConfirmedNotesImport(INotesService notes, IReadOnlyList<NoteInput> inputs)
    {
        this.notes = notes;
        Inputs = inputs;
    }

    public IReadOnlyList<NoteInput> Inputs { get; }

    public Task<IReadOnlyList<Note>> ConfirmAsync() => notes.ImportAsync(Inputs);
}
