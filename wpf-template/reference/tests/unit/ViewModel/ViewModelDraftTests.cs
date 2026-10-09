using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Shouldly;
using WpfNotesSample.Domain.Notes;
using WpfNotesSample.ViewModel;
using Xunit;

namespace WpfNotesSample.UnitTests.ViewModel;

public sealed class ViewModelDraftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSavePreservesTheDraftAndDisplaysTheError(bool conflict)
    {
        Exception error = conflict ? new NoteConflictException() : new NoteValidationException("入力を確認してください。");
        var service = new StubNotesService { Save = (_, _, _, _) => Task.FromException<Note>(error) };
        var saved = new Note(Guid.NewGuid(), "保存済みタイトル", "保存済み本文", 3, DateTime.UtcNow);
        var editor = new NoteEditViewModel(saved, service, null!, null!, new NavigationState())
        {
            NoteTitle = "作業中のタイトル",
            Body = "作業中の本文",
        };

        await editor.SaveCommand.ExecuteAsync(null);

        editor.NoteTitle.ShouldBe("作業中のタイトル");
        editor.Body.ShouldBe("作業中の本文");
        editor.IsDirty.ShouldBeTrue();
        editor.ErrorMessage.ShouldBe(error.Message);
        editor.CanInteract.ShouldBeTrue();
        editor.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task SuccessfulSaveUsesTheReturnedIdentityForTheNextSaveAndDiscardRestoresTheLatestSavedContent()
    {
        var id = Guid.NewGuid();
        var ids = new List<Guid?>();
        var versions = new List<int?>();
        var service = new StubNotesService
        {
            Save = (noteId, version, title, body) =>
            {
                ids.Add(noteId);
                versions.Add(version);
                return Task.FromResult(new Note(id, title.Trim(), body, ids.Count, DateTime.UtcNow));
            },
        };
        var editor = new NoteEditViewModel(service, null!, null!, new NavigationState())
        {
            NoteTitle = "  最初のタイトル  ",
            Body = "最初の本文",
        };

        await editor.SaveCommand.ExecuteAsync(null);
        editor.NoteTitle.ShouldBe("最初のタイトル");
        editor.IsDirty.ShouldBeFalse();
        editor.NoteTitle = "  更新したタイトル  ";
        editor.Body = "更新した本文";
        await editor.SaveCommand.ExecuteAsync(null);

        ids.ShouldBe(new Guid?[] { null, id });
        versions.ShouldBe(new int?[] { null, 1 });
        editor.IsDirty.ShouldBeFalse();
        editor.NoteTitle = "破棄するタイトル";
        editor.Body = "破棄する本文";
        editor.IsDirty.ShouldBeTrue();
        editor.DiscardCommand.Execute(null);

        editor.NoteTitle.ShouldBe("更新したタイトル");
        editor.Body.ShouldBe("更新した本文");
        editor.IsDirty.ShouldBeFalse();
        editor.ErrorMessage.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetailsLoadsTheRequestedNoteWithoutCreatingADraft()
    {
        var saved = new Note(Guid.NewGuid(), "詳細のタイトル", "詳細の本文", 2, DateTime.UtcNow);
        var service = new StubNotesService
        {
            Get = id =>
            {
                id.ShouldBe(saved.Id);
                return Task.FromResult<Note?>(saved);
            },
        };
        var navigation = new NavigationState();
        var details = new NoteDetailsViewModel(saved.Id, service, null!, null!, navigation);

        await details.OnNavigatedAsync(null!);

        navigation.CurrentPage.ShouldBeSameAs(details);
        details.NoteTitle.ShouldBe(saved.Title);
        details.Body.ShouldBe(saved.Body);
        details.EditNoteCommand.CanExecute(null).ShouldBeTrue();
        details.IsDirty.ShouldBeFalse();
        details.ErrorMessage.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailedDetailsCannotStartEditing(bool storageFailure)
    {
        var error = new SqliteException("詳細を読み込めません。", 10);
        var service = new StubNotesService
        {
            Get = _ => storageFailure ? Task.FromException<Note?>(error) : Task.FromResult<Note?>(null),
        };
        var details = new NoteDetailsViewModel(Guid.NewGuid(), service, null!, null!, new NavigationState());

        await details.OnNavigatedAsync(null!);

        details.NoteTitle.ShouldBeEmpty();
        details.Body.ShouldBeEmpty();
        details.EditNoteCommand.CanExecute(null).ShouldBeFalse();
        details.ErrorMessage.ShouldBe(storageFailure ? error.Message :
            "対象のノートは削除されています。一覧を読み直してください。");
        details.IsDirty.ShouldBeFalse();
        details.CanInteract.ShouldBeTrue();
    }

    [Fact]
    public async Task FailedRefreshPreservesThePreviouslyLoadedNotesAndDisplaysTheError()
    {
        var saved = new[]
        {
            new Note(Guid.NewGuid(), "表示済みノート1", "本文1", 1, DateTime.UtcNow),
            new Note(Guid.NewGuid(), "表示済みノート2", "本文2", 2, DateTime.UtcNow),
        };
        var service = new StubNotesService
        {
            List = () => Task.FromResult<IReadOnlyList<Note>>(saved),
        };
        var list = new NoteListViewModel(service, null!, null!, new NavigationState());
        await list.RefreshCommand.ExecuteAsync(null);
        list.Notes.ShouldBe(saved);
        var error = new SqliteException("一覧を読み込めません。", 10);
        service.List = () => Task.FromException<IReadOnlyList<Note>>(error);

        await list.RefreshCommand.ExecuteAsync(null);

        list.Notes.ShouldBe(saved);
        list.ErrorMessage.ShouldBe(error.Message);
        list.CanInteract.ShouldBeTrue();
        list.IsBusy.ShouldBeFalse();
    }

    private sealed class StubNotesService : INotesService
    {
        public Func<Guid?, int?, string, string, Task<Note>>? Save { get; set; }
        public Func<Guid, Task<Note?>>? Get { get; set; }
        public Func<Task<IReadOnlyList<Note>>>? List { get; set; }

        public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) =>
            Save?.Invoke(id, expectedVersion, title, body) ?? throw new NotSupportedException();

        public Task<Note?> GetAsync(Guid id) => Get?.Invoke(id) ?? throw new NotSupportedException();

        public Task<IReadOnlyList<Note>> ListAsync() =>
            List?.Invoke() ?? throw new NotSupportedException();
        public Task RemoveAsync(Guid id, int expectedVersion) => throw new NotSupportedException();
    }
}
