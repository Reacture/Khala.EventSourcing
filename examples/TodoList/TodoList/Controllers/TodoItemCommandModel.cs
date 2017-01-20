using System;
using System.ComponentModel.DataAnnotations;

namespace TodoList.Controllers
{
    public class TodoItemCommandModel
    {
        public Guid Id { get; set; }

        [Required]
        public string Description { get; set; }
    }
}
