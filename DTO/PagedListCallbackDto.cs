using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.DTO
{
    public class PagedListCallbackDto : ToDoListCallbackDto
    {
        public int Page { get; set; }

        public static new PagedListCallbackDto FromString(string input)
        {
            Helper.ValidateString(input);
            var parts = input.Split('|');
            //минимальное количество частей - 3
            if (parts.Length < 3)
                throw new FormatException("Неверный формат команды");
            var action = parts[0];
            var listIdPart = parts[1];
            var pagePart = parts[2];
            Guid? listId = null;
            if (!string.IsNullOrEmpty(listIdPart) && listIdPart != "null")
            {
                if (!Guid.TryParse(listIdPart, out var parsedId))
                    throw new FormatException("Неверный формат ID списка");
                listId = parsedId;
            }
            if (!int.TryParse(pagePart, out var page) || page < 0)
            throw new FormatException("Номер страницы должен быть неотрицательным числом");
            return new PagedListCallbackDto
            {
                Action = action,
                ToDoListId = listId,
                Page = page
            };
        }
        public override string ToString() => $"{Action}|{(ToDoListId?.ToString() ?? "null")}|{Page}";
    }
}
