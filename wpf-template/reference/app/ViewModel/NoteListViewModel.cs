using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Domain.Notes;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class NoteListViewModel : PageViewModel
{
    private readonly INotesService _notes;
    public NoteListViewModel([Inject] INotesService notes, [Inject] IPresentationService presentation,
        [Inject] IDialogService dialogs, [Inject] NavigationState navigation) : base(presentation, dialogs, navigation)
        => _notes = notes;

    public ObservableCollection<Note> Notes { get; } = new();

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunOperationAsync(async () =>
    {
        var loaded = await _notes.ListAsync();
        Notes.Clear();
        foreach (var note in loaded)
        {
            Notes.Add(note);
        }
    });

    [RelayCommand]
    private async Task NewNoteAsync() => await Presentation.NavigateToNoteEditAsync();

    [RelayCommand]
    private async Task ShowDetailsAsync(Note note) => await Presentation.NavigateToNoteDetailsAsync(note.Id);
}
