using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.DTO
{
    public class ToDoListCallbackDto : CallbackDto
    {
        public Guid? ToDoListId { get; set; }

    public static new ToDoListCallbackDto FromString(string input)
        {
            Helper.ValidateString(input);

            var parts = input.Split('|', 2);
            var dto = new ToDoListCallbackDto
            {
                Action = parts[0]
            };

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) && parts[1] != "null")
            {
                if (Guid.TryParse(parts[1], out var listId))
                {
                    dto.ToDoListId = listId;
                }
                else
                {
                    throw new FormatException("Неверный формат");
                }
            }
            return dto;
        }
        public override string ToString() =>
        ToDoListId.HasValue ? $"{Action}|{ToDoListId}" : $"{Action}|null";
    }
}
