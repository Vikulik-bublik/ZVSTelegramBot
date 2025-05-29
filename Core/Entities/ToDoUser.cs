using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Core.Entities

{
    public class ToDoUser
    {
        public Guid UserId { get; set; }
        public long TelegramUserId { get; set; }
        public string TelegramUserName { get; set; }
        public DateTime RegisteredAt { get; set; }
        public bool WaitingForMaxTaskCount { get; set; } = false; //для проверки состояния
        public bool WaitingForMaxLengthCount { get; set; } = false; //для проверки состояния
        public bool WaitingForConfigReset { get; set; } //для сброса состояния
        public int MaxTaskCount { get; set; }
        public int MaxLengthCount { get; set; }
    }
}
