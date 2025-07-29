using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Services;

namespace ZVSTelegramBot.Scenarios
{
    public class AddListScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoListService _toDoListService;

        public AddListScenario(IUserService userService, IToDoListService toDoListService)
        {
            _userService = userService;
            _toDoListService = toDoListService;
        }

        public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.AddList;

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                switch (context.CurrentStep)
                {
                    case null:
                        return await HandleInitial(bot, context, update, ct);

                    case "Name":
                        return await HandleNameInput(bot, context, update, ct);

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

            await bot.SendMessage(update.Message.Chat, "Введите название для нового списка задач:", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleNameInput(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                await Helper.ValidateString(update.Message.Text, ct);
                var listName = update.Message.Text.Trim();
                var user = (ToDoUser)context.Data["User"];
                var newList = await _toDoListService.Add(user, listName, ct);
                await bot.SendMessage(update.Message.Chat, $"Список <b>{Helper.EscapeMarkdownV2(newList.Name)}</b> успешно создан", parseMode: ParseMode.Html, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (ArgumentException ex)
            {
                await bot.SendMessage(update.Message.Chat, ex.Message, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Transition;
            }
        }
        private async Task HandleScenarioError(ITelegramBotClient bot, Update update, Exception ex, CancellationToken ct)
        {
            await bot.SendMessage(update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}
