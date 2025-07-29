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
using ZVSTelegramBot.DTO;
using ZVSTelegramBot.TelegramBot;

namespace ZVSTelegramBot.Scenarios
{
    public class AddTaskScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoService _toDoService;
        private readonly IToDoListService _toDoListService;

        public AddTaskScenario(IUserService userService, IToDoService toDoService, IToDoListService toDoListService)
        {
            _userService = userService;
            _toDoService = toDoService;
            _toDoListService = toDoListService;
        }

        public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.AddTask;

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                if (update.CallbackQuery != null)
                {
                    return await HandleListSelection(bot, context, update, ct);
                }

                if (update.Message.Text == null)
                    return ScenarioResult.Completed;

                switch (context.CurrentStep)
                {
                    case null:
                        return await HandleInitial(bot, context, update, ct);

                    case "Name":
                        return await HandleTaskName(bot, context, update, ct);

                    case "Deadline":
                        return await HandleDeadline(bot, context, update, ct);

                    default:
                        await bot.SendMessage(update.Message.Chat, "Неизвестный шаг сценария. Сброс...", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
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
                await bot.SendMessage(update.Message.Chat, "Пользователь не найден. Пожалуйста, зарегистрируйтесь с помощью /start", replyMarkup: Helper.GetUnauthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            context.Data["User"] = user;
            context.CurrentStep = "Name";

            await bot.SendMessage(update.Message.Chat, "Введите название задачи:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleTaskName(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            var message = update.Message.Text;
            await Helper.ValidateString(message, ct);

            context.Data["Name"] = message.Trim();
            context.CurrentStep = "ListSelection";
            var user = (ToDoUser)context.Data["User"];
            var lists = await _toDoListService.GetUserLists(user.UserId, ct);
            var tasksWithoutList = await _toDoService.GetByUserIdAndList(user.UserId, null, ct);

            await bot.SendMessage(update.Message.Chat, "Выберите список для задачи или выберите без списка:", replyMarkup: Helper.GetListSelectionKeyboard(lists.ToList(), tasksWithoutList.ToList(), hideManagementButtons: true), cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleListSelection(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery?.Data == null)
            {
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, "Ошибка: данные не получены", cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            var callbackDto = ToDoListCallbackDto.FromString(update.CallbackQuery.Data);

            if (callbackDto.Action != "show")
            {
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, "Неизвестное действие", cancellationToken: ct);
                return ScenarioResult.Transition;
            }

            context.Data["SelectedListId"] = callbackDto.ToDoListId;
            context.CurrentStep = "Deadline";

            string listName = callbackDto.ToDoListId.HasValue
                ? (await _toDoListService.Get(callbackDto.ToDoListId.Value, ct))?.Name ?? "Неизвестный список"
                : "Без списка";

            await bot.EditMessageText(
                update.CallbackQuery.Message.Chat,
                update.CallbackQuery.Message.MessageId,
                $"Вы выбрали список: <b>{Helper.EscapeMarkdownV2(listName)}</b>" +
                "\nТеперь введите дату выполнения в формате дд.мм.гггг или пропустить",
                parseMode: ParseMode.Html,
                replyMarkup: Helper.GetSkipKeyboard(),
                cancellationToken: ct);

            await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleDeadline(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            var message = update.Message.Text;
            await Helper.ValidateString(message, ct);
            var user = (ToDoUser)context.Data["User"];
            var taskName = (string)context.Data["Name"];
            var listId = (Guid?)context.Data["SelectedListId"];

            ToDoList list = null;
            if (listId.HasValue)
            {
                list = await _toDoListService.Get(listId.Value, ct);
            }

            DateTime? deadline = null;

            if (message.Equals("/skip", StringComparison.OrdinalIgnoreCase) ||
               (update.CallbackQuery?.Data == "skip"))
            {
                deadline = null;
            }
            else
            {
                if (!DateTime.TryParseExact(message, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    await bot.SendMessage(update.Message.Chat, "Неверный формат даты. Введите дату в формате дд.мм.гггг или пропустить", replyMarkup: Helper.GetSkipKeyboard(), cancellationToken: ct);
                    return ScenarioResult.Transition;
                }
                deadline = parsedDate;
            }

            var addedTask = await _toDoService.Add(user, taskName, deadline, list, ct);

            var result = $"✅ Задача добавлена\n" +
                $"📌 Название: <b>{Helper.EscapeMarkdownV2(addedTask.Name)}</b>\n" +
                $"🗂 Список: <b>{(list != null ? Helper.EscapeMarkdownV2(list.Name) : "Без списка")}</b>\n" +
                $"⏰ Дедлайн: <b>{(deadline.HasValue ? deadline.Value.ToString("dd.MM.yyyy") : "Не установлен")}</b>\n" +
                $"🆔 ID: <pre>{addedTask.Id.ToString()}</pre>";

            await bot.SendMessage(update.Message.Chat, result, parseMode: ParseMode.Html, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            return ScenarioResult.Completed;
        }

        private async Task HandleScenarioError(ITelegramBotClient bot, Update update, Exception ex, CancellationToken ct)
        {
            await bot.SendMessage(update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}