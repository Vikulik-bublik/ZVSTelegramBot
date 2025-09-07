using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;

namespace ZVSTelegramBot.Core.DataAccess.Models
{
    [Table("Notifications")]
    public class NotificationModel
    {
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("Type", Length = 100)]
        public string Type { get; set; }

        [Column("Text", Length = 500)]
        public string Text { get; set; }

        [Column("ScheduledAt")]
        public DateTime ScheduledAt { get; set; }

        [Column("IsNotified")]
        public bool IsNotified { get; set; }

        [Column("NotifiedAt")]
        public DateTime? NotifiedAt { get; set; }

        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; }
    }
}
