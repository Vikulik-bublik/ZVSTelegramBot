using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Core.Entities
{
    public enum ToDoItemState
    {
        Active,
        Completed
    }
    public class ToDoItem
    {
        public Guid Id { get; set; }
        public ToDoUser User { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ToDoItemState State { get; set; }
        public DateTime? StateChangedAt { get; set; }
        public DateTime? Deadline { get; set; }
        public ToDoList? List { get; set; }
    }
}
