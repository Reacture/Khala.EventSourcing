namespace Arcane.EventSourcing
{
    public interface IMemento
    {
        int Version { get; }
    }
}
