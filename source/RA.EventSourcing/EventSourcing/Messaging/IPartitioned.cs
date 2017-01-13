namespace ReactiveArchitecture.EventSourcing.Messaging
{
    public interface IPartitioned
    {
        string PartitionKey { get; }
    }
}
