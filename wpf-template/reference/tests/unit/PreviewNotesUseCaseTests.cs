using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using WpfNotesSample.Model.Domain.Notes;
using WpfNotesSample.Model.UseCase;
using Xunit;

namespace WpfNotesSample.UnitTests;

public sealed class PreviewNotesUseCaseTests
{
    [Fact]
    public async Task ConfirmationImportsTheNormalizedSnapshotDisplayedInThePreview()
    {
        var notes = new CapturingNotesService();
        var inputs = new List<NoteInput> { new NoteInput("  確認したタイトル  ", "本文\n") };
        var preview = new PreviewNotesUseCase(notes).Preview(inputs);

        inputs[0] = new NoteInput("確認後に変更したタイトル", "別の本文");
        preview.Inputs[0].Title.ShouldBe("確認したタイトル");
        preview.Inputs[0].Body.ShouldBe("本文\n");
        await preview.ConfirmAsync();

        notes.Imported.ShouldNotBeNull();
        notes.Imported![0].Title.ShouldBe(preview.Inputs[0].Title);
        notes.Imported[0].Body.ShouldBe(preview.Inputs[0].Body);
        notes.ImportCalls.ShouldBe(1);
    }

    [Fact]
    public void InvalidLaterInputPreventsCreatingAPreview()
    {
        var notes = new CapturingNotesService();
        var inputs = new[] { new NoteInput("正常", ""), new NoteInput("  ", "") };

        Should.Throw<NoteValidationException>(() => new PreviewNotesUseCase(notes).Preview(inputs));

        notes.ImportCalls.ShouldBe(0);
    }

    private sealed class CapturingNotesService : INotesService
    {
        public IReadOnlyList<NoteInput>? Imported { get; private set; }
        public int ImportCalls { get; private set; }

        public Task<IReadOnlyList<Note>> ImportAsync(IReadOnlyList<NoteInput> inputs)
        {
            Imported = inputs;
            ImportCalls++;
            return Task.FromResult<IReadOnlyList<Note>>(Array.Empty<Note>());
        }

        public Task<IReadOnlyList<Note>> ListAsync() => throw new NotSupportedException();
        public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) => throw new NotSupportedException();
        public Task RemoveAsync(Guid id, int expectedVersion) => throw new NotSupportedException();
    }
}
