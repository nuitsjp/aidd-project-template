using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Domain.Notes;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class NoteEditViewModel : PageViewModel
{
    private readonly INotesService _notes;
    private Guid? _id;
    private int? _version;
    private string _savedTitle;
    private string _savedBody;

    public NoteEditViewModel([Inject] INotesService notes, [Inject] IPresentationService presentation,
        [Inject] IDialogService dialogs, [Inject] NavigationState navigation) : this(null, notes, presentation, dialogs, navigation) { }

    public NoteEditViewModel(Note? note, [Inject] INotesService notes, [Inject] IPresentationService presentation,
        [Inject] IDialogService dialogs, [Inject] NavigationState navigation) : base(presentation, dialogs, navigation)
    {
        _notes = notes;
        _id = note?.Id;
        _version = note?.Version;
        _savedTitle = note?.Title ?? "";
        _savedBody = note?.Body ?? "";
        _noteTitle = _savedTitle;
        _body = _savedBody;
    }

    [ObservableProperty]
    private string _noteTitle;

    [ObservableProperty]
    private string _body;

    partial void OnNoteTitleChanged(string value) => UpdateDirty();
    partial void OnBodyChanged(string value) => UpdateDirty();
    private void UpdateDirty() => IsDirty = NoteTitle != _savedTitle || Body != _savedBody;

    [RelayCommand]
    private Task SaveAsync() => RunOperationAsync(async () =>
    {
        var saved = await _notes.SaveAsync(_id, _version, NoteTitle, Body);
        _id = saved.Id;
        _version = saved.Version;
        _savedTitle = saved.Title;
        _savedBody = saved.Body;
        NoteTitle = _savedTitle;
        Body = _savedBody;
        IsDirty = false;
        StatusMessage = "保存しました。";
    });

    [RelayCommand]
    private void Discard()
    {
        NoteTitle = _savedTitle;
        Body = _savedBody;
        IsDirty = false;
        ErrorMessage = "";
        StatusMessage = "変更を破棄しました。";
    }

    [RelayCommand]
    private async Task BackAsync() => await Presentation.NavigateToNoteListAsync();
}
