using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.DataAccess.Models
{
    [Table("ToDoUser")]
    public class ToDoUserModel
    {
        [PrimaryKey]
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("TelegramUserId")]
        public long TelegramUserId { get; set; }

        [Column("TelegramUserName", Length = 255)]
        public string TelegramUserName { get; set; }

        [Column("RegisteredAt")]
        public DateTime RegisteredAt { get; set; }
    }
}
