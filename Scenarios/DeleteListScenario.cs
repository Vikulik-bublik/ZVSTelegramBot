using System;
using System.Collections.Generic;
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

namespace ZVSTelegramBot.Scenarios
{
    public class DeleteListScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoListService _toDoListService;
        private readonly IToDoService _toDoService;

        public DeleteListScenario(IUserService userService, IToDoListService toDoListService, IToDoService toDoService)
        {
            _userService = userService;
            _toDoListService = toDoListService;
            _toDoService = toDoService;
        }

        public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.DeleteList;

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                switch (context.CurrentStep)
                {
                    case null:
                        return await HandleInitial(bot, context, update, ct);

                    case "Approve":
                        return await HandleApprove(bot, context, update, ct);

                    case "Delete":
                        return await HandleDelete(bot, context, update, ct);

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
                await bot.SendMessage(update.Message.Chat, "Пользователь не найден.  Пожалуйста, зарегистрируйтесь с помощью /start", replyMarkup: Helper.GetUnauthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            context.Data["User"] = user;
            var lists = await _toDoListService.GetUserLists(user.UserId, ct);
            if (lists.Count == 0)
            {
                await bot.SendMessage(update.Message.Chat, "У вас нет списков для удаления", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            await bot.SendMessage(update.Message.Chat, "Выберите список для удаления:", replyMarkup: Helper.GetListsKeyboard(lists, "DeleteList"), cancellationToken: ct);
            context.CurrentStep = "Approve";
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleApprove(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery == null)
            {
                await HandleInitial(bot, context, update, ct);
                return ScenarioResult.Transition;
            }

            var callbackDto = ToDoListCallbackDto.FromString(update.CallbackQuery.Data);
            var list = await _toDoListService.Get(callbackDto.ToDoListId.Value, ct);

            if (list == null)
            {
                await bot.SendMessage(update.Message.Chat, "Список не найден", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            context.Data["List"] = list;
            context.CurrentStep = "Delete";

            await bot.EditMessageText(
                update.CallbackQuery.Message.Chat,
                update.CallbackQuery.Message.MessageId,
                $"Подтверждаете удаление списка <b>{list.Name}</b> и всех его задач",
                parseMode: ParseMode.Html,
                replyMarkup: Helper.GetYesNoKeyboard(),
                cancellationToken: ct);

            await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleDelete(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery?.Data == null)
            {
                await bot.SendMessage(update.Message.Chat, "Неверный формат запроса", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            var response = update.CallbackQuery.Data;
            var user = (ToDoUser)context.Data["User"];
            var list = (ToDoList)context.Data["List"];

            if (response == "no")
            {
                await bot.EditMessageText(update.CallbackQuery.Message.Chat, update.CallbackQuery.Message.MessageId, "Удаление отменено", replyMarkup: null, cancellationToken: ct);
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            if (response == "yes")
            {
                try
                {
                    var tasks = await _toDoService.GetByUserIdAndList(user.UserId, list.Id, ct);
                    foreach (var task in tasks)
                    {
                        await _toDoService.Delete(task.Id, ct);
                    }

                    await _toDoListService.Delete(list.Id, ct);
                    await bot.EditMessageText(update.CallbackQuery.Message.Chat, update.CallbackQuery.Message.MessageId, $"Список <b>{list.Name}</b> и все его задачи: <b>{tasks.Count}</b> успешно удалены", parseMode: ParseMode.Html, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    await bot.EditMessageText(
                        update.CallbackQuery.Message.Chat,
                        update.CallbackQuery.Message.MessageId,
                        $"❌ Ошибка при удалении: {ex.Message}",
                        replyMarkup: null,
                        cancellationToken: ct);

                    return ScenarioResult.Completed;
                }
                finally
                {
                    await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
                }
                return ScenarioResult.Completed;
            }

            await bot.SendMessage(update.Message.Chat, "Неизвестная команда", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);

            return ScenarioResult.Completed;
        }
        private async Task HandleScenarioError(ITelegramBotClient bot, Update update, Exception ex, CancellationToken ct)
        {
            await bot.SendMessage(update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}
