namespace ReactiveArchitecture.EventSourcing
{
    public interface IMemento
    {
        int Version { get; }
    }
}
