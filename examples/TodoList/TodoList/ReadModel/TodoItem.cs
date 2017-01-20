using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoList.ReadModel
{
    public class TodoItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SequenceId { get; private set; }

        [Index(IsUnique = true)]
        public Guid Id { get; set; }

        [DisplayName("Date created")]
        public DateTimeOffset CreatedAt { get; set; }

        public string Description { get; set; }
    }
}
