namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System.Threading.Tasks;

    public interface ISqlEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        Task<T> FindByUniqueIndexedProperty(string name, string value);
    }
}
