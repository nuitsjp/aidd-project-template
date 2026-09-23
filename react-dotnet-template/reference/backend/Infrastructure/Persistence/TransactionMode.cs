namespace NotesSample.Infrastructure.Persistence;

internal enum TransactionMode
{
    Deferred,
    Immediate,
    Exclusive,
}
