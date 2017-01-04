namespace ReactiveArchitecture.EventSourcing
{
    public interface IMementoOriginator
    {
        IMemento SaveToMemento();
    }
}
