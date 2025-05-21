using System;
using Telegram.Bot;
using Telegram.Bot.Types;

public static class Helper
{
    public static int MaxTaskCount { get; set; }
    public static int MaxLengthCount { get; set; }
    public static async Task SetMaxTaskCount(ITelegramBotClient botClient, string input, Update update, CancellationToken ct)
    {
        try
        {
            MaxTaskCount = await ParseAndValidateInt(input, min: 1, max: 100, ct);
            await botClient.SendMessage(update.Message.Chat, $"Максимальное число задач установлено: {MaxTaskCount}.", cancellationToken: ct);
        }
        catch (ArgumentException ex)
        {
            throw ex;
        }
    }

    public static async Task SetMaxLengthCount(ITelegramBotClient botClient, string input, Update update, CancellationToken ct)
    {
        try
        {
            MaxLengthCount = await ParseAndValidateInt(input, min: 1, max: 100, ct);
            await botClient.SendMessage(update.Message.Chat, $"Максимальная длина задач установлена на количество символов: {MaxLengthCount}.", cancellationToken: ct);
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
            throw new ArgumentException("Ввод не должен быть пустым или содержать только пробелы.");
        }
        return Task.CompletedTask;
    }

    public static async Task<int> ParseAndValidateInt(string? str, int min, int max, CancellationToken ct)
    {
        await ValidateString(str, ct);

        if (!int.TryParse(str, out int result))
        {
            throw new ArgumentException("Ввод должен быть целым числом.");
        }
        if (result < min || result > max)
        {
            throw new ArgumentException($"Значение должно быть в диапазоне от {min} до {max}.");
        }
        return result;
    }
}
