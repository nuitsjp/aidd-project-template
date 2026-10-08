using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Model.Domain.Notes;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class NoteEditViewModel : PageViewModel
{
    private readonly INotesService notes;
    private Guid? id;
    private int? version;
    private string savedTitle;
    private string savedBody;

    public NoteEditViewModel([Inject] INotesService notes, [Inject] IPresentationService presentation,
        [Inject] NavigationState navigation) : this(null, notes, presentation, navigation) { }

    public NoteEditViewModel(Note? note, [Inject] INotesService notes,
        [Inject] IPresentationService presentation, [Inject] NavigationState navigation) : base(presentation, navigation)
    {
        this.notes = notes;
        id = note?.Id;
        version = note?.Version;
        savedTitle = note?.Title ?? "";
        savedBody = note?.Body ?? "";
        noteTitle = savedTitle;
        body = savedBody;
    }

    [ObservableProperty]
    private string noteTitle;

    [ObservableProperty]
    private string body;

    partial void OnNoteTitleChanged(string value) => UpdateDirty();
    partial void OnBodyChanged(string value) => UpdateDirty();
    private void UpdateDirty() => IsDirty = NoteTitle != savedTitle || Body != savedBody;

    [RelayCommand]
    private Task SaveAsync() => RunOperationAsync(async () =>
    {
        var saved = await notes.SaveAsync(id, version, NoteTitle, Body);
        id = saved.Id;
        version = saved.Version;
        savedTitle = saved.Title;
        savedBody = saved.Body;
        NoteTitle = savedTitle;
        Body = savedBody;
        IsDirty = false;
        StatusMessage = "保存しました。";
    });

    [RelayCommand]
    private void Discard()
    {
        NoteTitle = savedTitle;
        Body = savedBody;
        IsDirty = false;
        ErrorMessage = "";
        StatusMessage = "変更を破棄しました。";
    }

    [RelayCommand]
    private async Task BackAsync() => await Presentation.NavigateToNoteListAsync();
}
