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
using ZVSTelegramBot.DTO;

namespace ZVSTelegramBot.Scenarios
{
    public class DeleteTaskScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoService _toDoService;

        public DeleteTaskScenario(IUserService userService, IToDoService toDoService)
        {
            _userService = userService;
            _toDoService = toDoService;
        }

        public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.DeleteTask;

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                switch (context.CurrentStep)
                {
                    case null:
                        return await HandleInitial(bot, context, update, ct);

                    case "Confirm":
                        return await HandleConfirm(bot, context, update, ct);

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

            if (update.CallbackQuery == null)
            {
                await bot.SendMessage(update.Message.Chat, "Неверный запрос, выберите задачу для удаления.", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            var callbackDto = ToDoItemCallbackDto.FromString(update.CallbackQuery.Data);
            if (!callbackDto.ToDoItemId.HasValue)
            {
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, "Ошибка, ID задачи не указан", cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            var task = await _toDoService.Get(callbackDto.ToDoItemId.Value, ct);
            if (task == null)
            {
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, "Задача не найдена", cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            context.Data["Task"] = task;
            context.CurrentStep = "Confirm";

            await bot.EditMessageText(
                chatId: update.CallbackQuery.Message.Chat,
                messageId: update.CallbackQuery.Message.MessageId,
                text: $"Вы уверены, что хотите удалить задачу: <b>{Helper.EscapeMarkdownV2(task.Name)}</b>?",
                parseMode: ParseMode.Html,
                replyMarkup: Helper.GetYesNoKeyboard(),
                cancellationToken: ct);

            await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
            return ScenarioResult.Transition;
        }

        private async Task<ScenarioResult> HandleConfirm(ITelegramBotClient bot, ScenarioContext context, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery?.Data == null)
            {
                await bot.SendMessage(update.Message?.Chat, "Неверный формат запроса", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            var response = update.CallbackQuery.Data;
            var task = (ToDoItem)context.Data["Task"];

            if (response == "no")
            {
                await bot.EditMessageText(
                    update.CallbackQuery.Message.Chat,
                    update.CallbackQuery.Message.MessageId,
                    "Удаление отменено",
                    replyMarkup: null,
                    cancellationToken: ct);
                await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
                return ScenarioResult.Completed;
            }

            if (response == "yes")
            {
                try
                {
                    await _toDoService.Delete(task.Id, ct);
                    await bot.EditMessageText(
                        update.CallbackQuery.Message.Chat,
                        update.CallbackQuery.Message.MessageId,
                        $"Задача <b>{Helper.EscapeMarkdownV2(task.Name)}</b> успешно удалена",
                        parseMode: ParseMode.Html,
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    await bot.EditMessageText(
                        update.CallbackQuery.Message.Chat,
                        update.CallbackQuery.Message.MessageId,
                        $"❌ Ошибка при удалении: {ex.Message}",
                        replyMarkup: null,
                        cancellationToken: ct);
                }
                finally
                {
                    await bot.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
                }
                return ScenarioResult.Completed;
            }
            await bot.SendMessage( update.Message.Chat, "Неизвестная команда", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            return ScenarioResult.Completed;
        }

        private async Task HandleScenarioError(ITelegramBotClient bot, Update update, Exception ex, CancellationToken ct)
        {
            await bot.SendMessage(update.Message.Chat, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}
