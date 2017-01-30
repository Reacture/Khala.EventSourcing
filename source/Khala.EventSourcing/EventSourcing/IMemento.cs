namespace Khala.EventSourcing
{
    public interface IMemento
    {
        int Version { get; }
    }
}
