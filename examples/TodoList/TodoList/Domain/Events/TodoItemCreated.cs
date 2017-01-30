using Khala.EventSourcing;

namespace TodoList.Domain.Events
{
    public class TodoItemCreated : DomainEvent
    {
        public string Description { get; set; }
    }
}
