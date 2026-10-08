using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WpfNotesSample.Model.Domain.Notes;
using WpfNotesSample.Model.UseCase;

namespace WpfNotesSample.ViewModel;

[Navigate]
public partial class ImportNotesViewModel : PageViewModel
{
    private readonly PreviewNotesUseCase previewNotes;
    private ConfirmedNotesImport? confirmation;

    public ImportNotesViewModel([Inject] PreviewNotesUseCase previewNotes,
        [Inject] IPresentationService presentation, [Inject] NavigationState navigation) : base(presentation, navigation)
        => this.previewNotes = previewNotes;

    [ObservableProperty]
    private string input = "";

    public ObservableCollection<NoteInput> PreviewItems { get; } = new();

    [ObservableProperty]
    private bool isConfirmed;

    partial void OnInputChanged(string value)
    {
        confirmation = null;
        PreviewItems.Clear();
        IsConfirmed = false;
        IsDirty = !string.IsNullOrWhiteSpace(value);
        StatusMessage = "";
        ErrorMessage = "";
    }

    [RelayCommand]
    private void Preview()
    {
        ErrorMessage = "";
        StatusMessage = "";
        try
        {
            var inputs = new List<NoteInput>();
            foreach (var line in Input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var delimiter = line.IndexOf('\t');
                inputs.Add(delimiter < 0 ? new NoteInput(line, "") : new NoteInput(line.Substring(0, delimiter), line.Substring(delimiter + 1)));
            }
            confirmation = previewNotes.Preview(inputs);
            PreviewItems.Clear();
            foreach (var item in confirmation.Inputs) PreviewItems.Add(item);
            IsConfirmed = true;
            StatusMessage = $"{PreviewItems.Count} 件を確認しました。内容が正しければ一括登録してください。";
        }
        catch (NoteValidationException error)
        {
            confirmation = null;
            PreviewItems.Clear();
            IsConfirmed = false;
            ErrorMessage = error.Message;
        }
    }

    [RelayCommand]
    private Task ConfirmAsync() => RunOperationAsync(async () =>
    {
        if (confirmation == null) throw new NoteValidationException("入力内容をもう一度確認してください。");
        var saved = await confirmation.ConfirmAsync();
        Input = "";
        StatusMessage = $"{saved.Count} 件を登録しました。";
    });
}
