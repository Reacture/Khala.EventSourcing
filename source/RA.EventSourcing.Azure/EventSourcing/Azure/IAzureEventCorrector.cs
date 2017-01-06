namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Threading.Tasks;

    public interface IAzureEventCorrector
    {
        Task CorrectEvents<T>(Guid sourceId)
            where T : class, IEventSourced;
    }
}
