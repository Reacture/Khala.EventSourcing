using ReactiveArchitecture.EventSourcing;

namespace TodoList.Events
{
    public class TodoItemCreated : DomainEvent
    {
        public string Description { get; set; }
    }
}
