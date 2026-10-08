namespace WpfNotesSample.Model.Domain.Notes;

public sealed class NoteInput
{
    public NoteInput(string title, string body)
    {
        Title = title;
        Body = body;
    }

    public string Title { get; }
    public string Body { get; }
}
