using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.TelegramBot;

namespace ZVSTelegramBot.Scenarios
{
    public class AddTaskScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoService _toDoService;

        public AddTaskScenario(IUserService userService, IToDoService toDoService)
        {
            _userService = userService;
            _toDoService = toDoService;
        }

        public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.AddTask;

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                var message = update.Message?.Text;
                if (message == null)
                return ScenarioResult.Transition;

                switch (context.CurrentStep)
                {
                    case null: //инициализация
                        return await HandleInitial(bot, context, update, ct);

                    case "Name": //обработка названия задачи
                        return await HandleTaskName(bot, context, update, message, ct);
                    
                    case "Deadline": //обработка дедлайна
                        return await HandleDeadline(bot, context, update, message, ct);

                    default:
                        await bot.SendMessage(update.Message.Chat, "Неизвестный шаг сценария. Сброс...", cancellationToken: ct);
                        return ScenarioResult.Completed;
                }
            }
            catch (Exception ex)
            {
                await HandleScenarioError(bot, update, ex, ct);
                return ScenarioResult.Completed;
            }
        }

        private async Task<ScenarioResult> HandleInitial(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUser(context.UserId, ct);
            if (user == null)
            {
                await bot.SendMessage(update.Message.Chat, "Ошибка: Пользователь не найден!", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            context.Data["User"] = user;
            context.CurrentStep = "Name";

            await bot.SendMessage(update.Message.Chat, "Введите название задачи:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleTaskName(ITelegramBotClient bot, ScenarioContext context, Update update, string taskName, CancellationToken ct)
        {
            await Helper.ValidateString(taskName, ct);

            context.Data["Name"] = taskName;
            context.CurrentStep = "Deadline";

            await bot.SendMessage(update.Message.Chat, "Введите дедлайн задачи в формате ДД.ММ.ГГГГ:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleDeadline(ITelegramBotClient bot, ScenarioContext context, Update update, string message, CancellationToken ct)
        {
            DateTime deadline;

            if (!DateTime.TryParseExact(message, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out deadline))
            {
                await bot.SendMessage(update.Message.Chat, "Неверный формат даты. Пожалуйста, введите дату в формате ДД.ММ.ГГГГ:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
                return ScenarioResult.Transition;
            }

            if (deadline.Date < DateTime.Today)
            {
                await bot.SendMessage(update.Message.Chat, "Дедлайн не может быть в прошлом. Введите корректную дату:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
                return ScenarioResult.Transition;
            }

            var user = (ToDoUser)context.Data["User"];
            var taskName = (string)context.Data["Name"];
            var addedTask = await _toDoService.Add(user, taskName, deadline, ct);

            var result = $"Задача добавлена" +
                $"\nНазвание: {Helper.EscapeMarkdownV2(addedTask.Name)}" +
                $"\nДедлайн: {Helper.EscapeMarkdownV2(deadline.ToString("dd:MM:yyyy"))}" +
                $"\nID: `{Helper.EscapeMarkdownV2(addedTask.Id.ToString())}`";

            await bot.SendMessage(update.Message.Chat, result, parseMode: ParseMode.MarkdownV2, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            return ScenarioResult.Completed;
        }

        private async Task HandleScenarioError(ITelegramBotClient bot, Update update, Exception ex, CancellationToken ct)
        {
            await bot.SendMessage(update.Message.Chat, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}
