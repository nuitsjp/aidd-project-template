using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;

namespace WpfNotesSample.ViewModel;

public abstract partial class PageViewModel : ObservableObject, INavigatedAsyncAware, IPausingAware, IDisposingAware
{
    private readonly NavigationState _navigation;

    protected PageViewModel(IPresentationService presentation, IDialogService dialogs, NavigationState navigation)
    {
        Presentation = presentation;
        Dialogs = dialogs;
        _navigation = navigation;
    }

    protected IPresentationService Presentation { get; }
    protected IDialogService Dialogs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public bool CanInteract => !IsBusy;

    public async Task OnNavigatedAsync(PostForwardEventArgs args)
    {
        _navigation.CurrentPage = this;
        await LoadAsync();
    }

    public void OnPausing(PreForwardEventArgs args) => args.Cancel = !CanLeave();
    public void OnDisposing(PreBackwardEventArgs args) => args.Cancel = !CanLeave();

    public bool CanLeave()
    {
        if (IsBusy)
        {
            return false;
        }

        return !IsDirty || Dialogs.Confirm("変更の破棄", "保存していない変更を破棄しますか？");
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    [SuppressMessage("Minor Code Smell", "S2221", Justification = "画面操作の失敗はすべて原因を表示して操作を続けられるようにする受け口。")]
    protected async Task RunOperationAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = "";
        StatusMessage = "";
        try
        {
            await operation();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ErrorMessage = error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
