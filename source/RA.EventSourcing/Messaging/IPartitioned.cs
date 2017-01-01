namespace ReactiveArchitecture.Messaging
{
    public interface IPartitioned
    {
        string PartitionKey { get; }
    }
}
