using Arcane.EventSourcing;

namespace TodoList.Domain.Events
{
    public class TodoItemUpdated : DomainEvent
    {
        public string Description { get; set; }
    }
}
