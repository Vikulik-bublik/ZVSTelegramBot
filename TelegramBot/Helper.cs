using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using ZVSTelegramBot.Core.Entities;

public static class Helper
{
    public static async Task SetMaxTaskCount(ITelegramBotClient botClient, string input, ToDoUser user, Update update, CancellationToken ct)
    {
        try
        {
            user.MaxTaskCount = await ParseAndValidateInt(input, min: 1, max: 100, ct);
            await botClient.SendMessage(update.Message.Chat, $"Максимальное число задач установлено: {user.MaxTaskCount}", cancellationToken: ct);
        }
        catch (ArgumentException ex)
        {
            throw ex;
        }
    }
    public static async Task SetMaxLengthCount(ITelegramBotClient botClient, string input, ToDoUser user, Update update, CancellationToken ct)
    {
        try
        {
            user.MaxLengthCount = await ParseAndValidateInt(input, min: 1, max: 100, ct);
            await botClient.SendMessage(update.Message.Chat, $"Максимальная длина задач установлена на количество символов: {user.MaxLengthCount}", cancellationToken: ct);
        }
        catch (ArgumentException ex)
        {
            throw ex;
        }
    }
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
}
