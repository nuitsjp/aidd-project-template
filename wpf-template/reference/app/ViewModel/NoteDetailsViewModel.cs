using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Domain.Notes;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class NoteDetailsViewModel : PageViewModel
{
    private readonly Guid _id;
    private readonly INotesService _notes;

    public NoteDetailsViewModel(Guid id, [Inject] INotesService notes,
        [Inject] IPresentationService presentation, [Inject] IDialogService dialogs, [Inject] NavigationState navigation)
        : base(presentation, dialogs, navigation)
    {
        _id = id;
        _notes = notes;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteTitle))]
    [NotifyPropertyChangedFor(nameof(Body))]
    [NotifyCanExecuteChangedFor(nameof(EditNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteNoteCommand))]
    private Note? _note;

    public string NoteTitle => Note?.Title ?? "";
    public string Body => Note?.Body ?? "";
    private bool HasNote => Note != null;

    protected override Task LoadAsync() => RunOperationAsync(async () =>
    {
        Note = null;
        Note = await _notes.GetAsync(_id)
            ?? throw new NoteValidationException("対象のノートは削除されています。一覧を読み直してください。");
    });

    [RelayCommand(CanExecute = nameof(HasNote))]
    private async Task EditNoteAsync() => await Presentation.NavigateToNoteEditAsync(Note!);

    [RelayCommand(CanExecute = nameof(HasNote))]
    private async Task DeleteNoteAsync()
    {
        var deleted = false;
        await RunOperationAsync(async () =>
        {
            if (!Dialogs.Confirm("ノートの削除", $"「{Note!.Title}」を削除しますか？"))
            {
                return;
            }

            await _notes.RemoveAsync(Note.Id, Note.Version);
            deleted = true;
        });
        // 実行中は画面離脱を拒否するため、削除の完了後に一覧へ移動する。
        if (deleted)
        {
            await Presentation.NavigateToNoteListAsync();
        }
    }

    [RelayCommand]
    private async Task BackAsync() => await Presentation.NavigateToNoteListAsync();
}
