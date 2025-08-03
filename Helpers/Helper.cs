using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.DTO;

public static class Helper
{
    private const string AddListButtonText = "🆕 Добавить список";
    private const string DeleteListButtonText = "❌ Удалить список";
    private const string NoListButtonText = "📌 Без списка";
    private const string SkipButtonText = "⏩ Пропустить";
    private const string CompleteTaskButtonText = "✅ Выполнить";
    private const string DeleteTaskButtonText = "❌ Удалить";
    public const string ViewCompletedButtonText = "☑️ Посмотреть выполненные";
    public const string BackToActiveButtonText = "⬅️ К активным задачам";
    //метод валидации ввода
    public static Task ValidateString(string? str, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentException("Ввод не должен быть пустым или содержать только пробелы");
        }
        return Task.CompletedTask;
    }
    //метод создания кнопки команды start для незарегистрированных
    public static ReplyKeyboardMarkup GetUnauthorizedKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("/start")
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }
    //метод создания кнопок команд addtask, show, report для зарегистрированных
    public static ReplyKeyboardMarkup GetAuthorizedKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("/addtask"), new KeyboardButton("/show") },
            new[] { new KeyboardButton("/report") }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }
    //метод создания кнопки команды cancel
    public static ReplyKeyboardMarkup GetCancelKeyboard()
    {
        return new ReplyKeyboardMarkup(new[] { new KeyboardButton("/cancel") })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }
    //метод создания кнопки команды skip
    public static InlineKeyboardMarkup GetSkipKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(SkipButtonText, "skip")
            }
        });
    }
    //кнопки для выбора списка
    public static InlineKeyboardMarkup GetListSelectionKeyboard(List<ToDoList> lists, List<ToDoItem> tasksWithoutList, bool hideManagementButtons = false)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        //кнопка Без списка
        buttons.Add(new[]
        {
        InlineKeyboardButton.WithCallbackData(
            NoListButtonText,
            new ToDoListCallbackDto { Action = "show", ToDoListId = null }.ToString())
    });

        //кнопки для каждого списка
        buttons.AddRange(lists.Select(list => new[]
        {
        InlineKeyboardButton.WithCallbackData(
            list.Name,
            new ToDoListCallbackDto { Action = "show", ToDoListId = list.Id }.ToString())
    }));

        //кнопки добавить-удалить список
        if (!hideManagementButtons)
        {
            buttons.Add(new[]
            {
            InlineKeyboardButton.WithCallbackData(
                AddListButtonText,
                new ToDoListCallbackDto { Action = "addlist" }.ToString()),
            InlineKeyboardButton.WithCallbackData(
                DeleteListButtonText,
                new ToDoListCallbackDto { Action = "deletelist" }.ToString())
        });
        }

        return new InlineKeyboardMarkup(buttons);
    }
    //кнопка для удаления списка
    public static InlineKeyboardMarkup GetListsKeyboard(IEnumerable<ToDoList> lists, string action)
    {
        var buttons = lists.Select(list => new[]
        {
            InlineKeyboardButton.WithCallbackData(
                list.Name,
                new ToDoListCallbackDto { Action = action, ToDoListId = list.Id }.ToString())
        }).ToArray();

        return new InlineKeyboardMarkup(buttons);
    }
    //клавиатура Да-Нет
    public static InlineKeyboardMarkup GetYesNoKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("Да", "yes"),
            InlineKeyboardButton.WithCallbackData("Нет", "no")
        }
        });
    }
    //кнопки завершить-удалить задачу
    public static InlineKeyboardMarkup GetTaskActionsKeyboard(Guid taskId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(CompleteTaskButtonText,
                new ToDoItemCallbackDto { Action = "completetask", ToDoItemId = taskId }.ToString()),
                InlineKeyboardButton.WithCallbackData(DeleteTaskButtonText,
                new ToDoItemCallbackDto { Action = "deletetask", ToDoItemId = taskId }.ToString())
            }
        });
    }
    //экранируем спецсимволы, иначе вылетает ошибка у Бота
    public static string EscapeMarkdownV2(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text.Replace("_", "\\_")
                   .Replace("*", "\\*")
                   .Replace("[", "\\[")
                   .Replace("]", "\\]")
                   .Replace("(", "\\(")
                   .Replace(")", "\\)")
                   .Replace("~", "\\~")
                   .Replace("`", "\\`")
                   .Replace(">", "\\>")
                   .Replace("#", "\\#")
                   .Replace("+", "\\+")
                   .Replace("-", "\\-")
                   .Replace("=", "\\=")
                   .Replace("|", "\\|")
                   .Replace("{", "\\{")
                   .Replace("}", "\\}")
                   .Replace("!", "\\!")
                   .Replace(".", "\\.")
                   .Replace("\\", "\\\\");
    }
}