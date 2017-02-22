using Khala.EventSourcing;

namespace TodoList.Domain
{
    public class TodoItemMemento : IMemento
    {
        public int Version { get; set; }

        public string Description { get; set; }

        public bool IsDeleted { get; set; }
    }
}