using System;

namespace WpfNotesSample.Domain.Notes;

public sealed record Note(Guid Id, string Title, string Body, int Version, DateTime UpdatedAt);
