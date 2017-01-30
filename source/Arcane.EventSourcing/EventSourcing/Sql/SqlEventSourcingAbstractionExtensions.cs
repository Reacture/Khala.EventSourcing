namespace Arcane.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class SqlEventSourcingAbstractionExtensions
    {
        public static Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            this ISqlEventSourcedRepository<T> repository,
            string name,
            string value)
            where T : class, IEventSourced
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return repository.FindIdByUniqueIndexedProperty(name, value, CancellationToken.None);
        }
    }
}
