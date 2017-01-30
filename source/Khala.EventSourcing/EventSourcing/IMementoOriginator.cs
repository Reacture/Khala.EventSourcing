namespace Khala.EventSourcing
{
    public interface IMementoOriginator
    {
        IMemento SaveToMemento();
    }
}
