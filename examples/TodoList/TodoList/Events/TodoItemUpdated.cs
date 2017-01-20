using ReactiveArchitecture.EventSourcing;

namespace TodoList.Events
{
    public class TodoItemUpdated : DomainEvent
    {
        public string Description { get; set; }
    }
}
