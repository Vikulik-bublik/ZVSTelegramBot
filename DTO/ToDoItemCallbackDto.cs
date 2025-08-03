using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.DTO
{
    public class ToDoItemCallbackDto : CallbackDto
    {
        public Guid? ToDoItemId { get; set; }

        public static new ToDoItemCallbackDto FromString(string input)
        {
            Helper.ValidateString(input);
            var parts = input.Split('|', 2);
            if (parts.Length < 2)
            throw new FormatException("Неверный формат входной строки");
            var dto = new ToDoItemCallbackDto
            {
                Action = parts[0]
            };
            if (!string.IsNullOrWhiteSpace(parts[1]) && parts[1] != "null")
            {
                if (!Guid.TryParse(parts[1], out var itemId))
                throw new FormatException("Неверный формат Id");
                dto.ToDoItemId = itemId;
            }
            return dto;
        }
        public override string ToString() => ToDoItemId.HasValue? $"{base.ToString()}|{ToDoItemId.Value}" : $"{base.ToString()}|null";
    }
}
