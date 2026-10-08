using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Model.Domain.Notes;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class NoteListViewModel : PageViewModel
{
    private readonly INotesService notes;
    public NoteListViewModel([Inject] INotesService notes, [Inject] IPresentationService presentation,
        [Inject] NavigationState navigation) : base(presentation, navigation) => this.notes = notes;

    public ObservableCollection<Note> Notes { get; } = new();

    [ObservableProperty]
    private Note? selectedNote;

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunOperationAsync(async () =>
    {
        var loaded = await notes.ListAsync();
        Notes.Clear();
        foreach (var note in loaded) Notes.Add(note);
        StatusMessage = $"{Notes.Count} 件のメモ";
    });

    [RelayCommand]
    private async Task NewNoteAsync() => await Presentation.NavigateToNoteEditAsync();

    [RelayCommand]
    private async Task EditNoteAsync()
    {
        if (SelectedNote != null) await Presentation.NavigateToNoteEditAsync(SelectedNote);
    }

    [RelayCommand]
    private Task DeleteNoteAsync() => RunOperationAsync(async () =>
    {
        var note = SelectedNote;
        if (note == null) return;
        if (Presentation.ShowMessage($"「{note.Title}」を削除しますか？", "メモの削除",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        await notes.RemoveAsync(note.Id, note.Version);
        Notes.Remove(note);
        StatusMessage = "削除しました。";
    });
}
