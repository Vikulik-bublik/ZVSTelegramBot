using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.Entities;

public static class Helper
{
    public static Task ValidateString(string? str, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentException("Ввод не должен быть пустым или содержать только пробелы");
        }
        return Task.CompletedTask;
    }
    public static async Task<int> ParseAndValidateInt(string? str, int min, int max, CancellationToken ct)
    {
        await ValidateString(str, ct);

        if (!int.TryParse(str, out int result))
        {
            throw new ArgumentException("Ввод должен быть целым числом");
        }
        if (result < min || result > max)
        {
            throw new ArgumentException($"Значение должно быть в диапазоне от {min} до {max}");
        }
        return result;
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
    //метод создания кнопок команд addtask, showtasks, showalltasks, report для зарегистрированных
    public static ReplyKeyboardMarkup GetAuthorizedKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("/addtask"), new KeyboardButton("/showtasks") },
            new[] { new KeyboardButton("/showalltasks"), new KeyboardButton("/report") }
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
                   //.Replace("-", "\\-")
                   .Replace("=", "\\=")
                   .Replace("|", "\\|")
                   .Replace("{", "\\{")
                   .Replace("}", "\\}")
                   .Replace("!", "\\!")
                   .Replace(".", "\\.")
                   .Replace("\\", "\\\\");
    }
}
