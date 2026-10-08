using System.Collections.Generic;
using WpfNotesSample.Model.Domain.Notes;

namespace WpfNotesSample.Model.UseCase;

public sealed class PreviewNotesUseCase
{
    private readonly INotesService notes;

    public PreviewNotesUseCase(INotesService notes)
    {
        this.notes = notes;
    }

    public ConfirmedNotesImport Preview(IReadOnlyList<NoteInput> inputs) =>
        new ConfirmedNotesImport(notes, NoteRules.NormalizeAll(inputs));
}
