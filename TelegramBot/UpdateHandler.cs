using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Exceptions;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.DTO;
using ZVSTelegramBot.Helpers;
using ZVSTelegramBot.Scenarios;

namespace ZVSTelegramBot.TelegramBot
{
    public delegate void MessageEventHandler(string message);
    public class UpdateHandler : IUpdateHandler
    {
        public event MessageEventHandler OnHandleUpdateStarted;
        public event MessageEventHandler OnHandleUpdateCompleted;
        private readonly IUserService _userService;
        private readonly IToDoService _toDoService;
        private readonly IToDoReportService _reportService;
        private readonly IEnumerable<IScenario> _scenarios;
        private readonly IScenarioContextRepository _contextRepository;
        private readonly IToDoListService _toDoListService;
        private static readonly int _pageSize = 5;

        public UpdateHandler(IUserService userService, IToDoService toDoService, IToDoReportService reportService, IEnumerable<IScenario> scenarios, IToDoListService toDoListService, IScenarioContextRepository contextRepository)
        {
            _userService = userService;
            _toDoService = toDoService;
            _reportService = reportService;
            _scenarios = scenarios;
            _contextRepository = contextRepository;
            _toDoListService = toDoListService;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            //обработка нажатия на Inline кнопки
            await (update switch
            {
                { Message: { } message } => OnMessage(botClient, update, ct),
                { CallbackQuery: { } callbackQuery } => OnCallbackQuery(botClient, callbackQuery, ct),
                _ => OnUnknown(botClient, update, ct)
            });
        }
        private async Task OnMessage(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            //что-бы не вылетал эксепшн при отправке сайлов, фоток итд
            var message = update.Message;
            if (message.Text == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пожалуйста, используйте текстовые сообщения", cancellationToken: ct);
                return;
            }

            OnHandleUpdateStarted?.Invoke(message.Text);

            try
            {
                var telegramUserId = message.From.Id;
                var context = await _contextRepository.GetContext(telegramUserId, ct);

                if (message.Text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    if (context != null)
                    {
                        await _contextRepository.ResetContext(telegramUserId, ct);
                        await botClient.SendMessage(update.Message.Chat, "Текущее действие отменено", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                    }
                    return;
                }

                if (context != null)
                {
                    await ProcessScenario(botClient, context, update, ct);
                    return;
                }

                var user = await _userService.GetUser(telegramUserId, ct);
                var command = message.Text.Split(' ')[0];

                if (command == "/start")
                {
                    await Start(botClient, user, update, ct);
                    return;
                }

                if (user != null)
                {
                    await HandleRegisteredUserCommands(botClient, command, user, update, ct);
                }
                else
                {
                    await HandleUnregisteredUserCommands(botClient, command, update, ct);
                }
            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, update, ct);
            }
            finally
            {
                OnHandleUpdateCompleted?.Invoke(message.Text);
            }
        }
        private async Task OnUnknown(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            Console.WriteLine($"Получен неизвестный тип update: {update.Type}");
            long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            if (chatId.HasValue)
            {
                await botClient.SendMessage(chatId.Value, "Извините, я не могу обработать этот тип сообщения", cancellationToken: ct);
            }
        }
  
        //нужен ли этот метод???? это вроде из старых ДЗ
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken ct)
        {
            Console.WriteLine($"HandleError: {exception.Message})");
            return Task.CompletedTask;
        }
        
        //метод обработки команды start-регистрация
        private async Task Start(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            try
            {
                if (user == null)
                {
                    user = await _userService.RegisterUser(update.Message.From.Id, update.Message.From.Username, ct);
                    await botClient.SendMessage(update.Message.Chat, $"Добро пожаловать, Вы зарегистрированы как {user.TelegramUserName}!", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(update.Message.Chat, $"Мы уже знакомы, {user.TelegramUserName}!", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                }

            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, update, ct);
            }
        }
        //кейсы обработки команд зарегистрированных пользователей
        private async Task HandleRegisteredUserCommands(ITelegramBotClient botClient, string command, ToDoUser? user, Update update, CancellationToken ct)
        {
            switch (command)
            {
                case "/info":
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.3.5 от 07.09.2025", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                    break;
                case "/help":
                    await Help(botClient, update, ct);
                    break;
                case "/addtask":
                    await AddTask(botClient, user, update,ct);
                    break;
                case "/show":
                    await Show(botClient, user.UserId, update, ct);
                    break;
                case "/report":
                    await Report(botClient, user, update, ct);
                    break;
                case "/find":
                    await Find(botClient, user, update, ct);
                    break;
                default:
                    await botClient.SendMessage(update.Message.Chat, "Введена неверная команда. Пожалуйста, попробуйте снова", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                    break;
            }
        }
        //обработка команд незарегистрированных пользователей
        private async Task HandleUnregisteredUserCommands(ITelegramBotClient botClient, string command, Update update, CancellationToken ct)
        {
            if (command == "/info" || command == "/help")
            {
                if (command == "/info")
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.3.5 от 07.09.2025" +
                        "\nПожалуйста, зарегистрируйтесь, нажав кнопку /start", replyMarkup: Helper.GetUnauthorizedKeyboard(), cancellationToken: ct);

                if (command == "/help")
                    await Help(botClient, update, ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Вы не зарегистрированы" +
                    "\nНезарегистрированным пользователям доступны команды только /help и /info. \nПожалуйста, зарегистрируйтесь, нажав кнопку /start", replyMarkup: Helper.GetUnauthorizedKeyboard(), cancellationToken: ct);
            }
        }
        //общий Хелп для всех категорий пользователей. Однако добавлено различие в кнопках
        private async Task Help(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUser(update.Message.From.Id, ct);
            bool isRegistered = user != null;
            var helpMessage = "Доступные команды:" +
                              "\n/start, /help, /info" +
                              "\n\nДля зарегистрированных пользователей доступны:" +
                              "\n/start - запуск процедуры регистрации пользователя." +
                              "\n/addtask - добавление в список дел новой задачи" +
                              "\n/show - показать списки с задачам" +
                              "\n/report - вывод статистики по задачам" +
                              "\n/find <префикс> - поиск задачи по нескольким сиволам ее начала" +
                              "\n/cancel - отмена текущего действия";
            if (!isRegistered)
            {
                await botClient.SendMessage(update.Message.Chat, $"{helpMessage}" +
                    $"\nПожалуйста, зарегистрируйтесь, нажав кнопку /start", replyMarkup: Helper.GetUnauthorizedKeyboard(),cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, helpMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            }
        }
        //добавляем задачу по имени
        private async Task AddTask(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            var context = new ScenarioContext(update.Message.From.Id, ScenarioType.AddTask);
            await _contextRepository.SetContext(context.UserId, context, ct);
            await ProcessScenario(botClient, context, update, ct);
        }
        //показываем активные списки
        private async Task Show(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            try
            {
                var lists = await _toDoListService.GetUserLists(userId, ct);
                var (_, tasksWithoutList) = await GetFormattedTasksList(userId, null, ct);

                var keyboard = Helper.GetListSelectionKeyboard(lists, tasksWithoutList);

                await botClient.SendMessage(update.Message.Chat, "Выберите список:", replyMarkup: keyboard, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, update, ct);
            }
        }
        //вывод статистики по задачам
        private async Task Report(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var (total, completed, active, generatedAt) = await _reportService.GetUserStats(user.UserId, ct);
            var report = $"Статистика по задачам на {generatedAt}" +
                $"\nВсего: {total}; Завершенных: {completed}; Активных: {active}";
            await botClient.SendMessage(update.Message.Chat, report, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
        //поиск задачи по префиксу
        private async Task Find(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var namePrefix = update.Message.Text.Substring(5).Trim();
            if (string.IsNullOrWhiteSpace(namePrefix))
            {
                await botClient.SendMessage(update.Message.Chat, "Для поиска задачи введите ключевое слово после команды /find", cancellationToken: ct);
                return;
            }
            var tasks = await _toDoService.Find(user, namePrefix, ct);
            var taskList = string.Join(Environment.NewLine, tasks.Select(t =>
            {
                var deadlineInfo = t.Deadline.HasValue
                    ? $"\n⏰ Дедлайн: <b>{t.Deadline.Value.ToString("dd.MM.yyyy")}</b>"
                    : "Не задан";

                return $"\n🗂 Задача: <b>{Helper.EscapeMarkdownV2(t.Name)}</b>, статус: <b>{Helper.EscapeMarkdownV2(t.State.ToString())}</b>" +
                       $"\n⏰ Время создания: <b>{t.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss")}</b>" +
                       deadlineInfo +
                       $"\n🆔 ID: <pre>{Helper.EscapeMarkdownV2(t.Id.ToString())}</pre>";
            }));
            await botClient.SendMessage(update.Message.Chat,
                string.IsNullOrEmpty(taskList)
                ? $"Не найдено задач, начинающихся с <b>{namePrefix}</b>"
                : $"По вашему запросу найдены следующие задачи:\n{taskList}", replyMarkup: Helper.GetAuthorizedKeyboard(),parseMode: ParseMode.Html, cancellationToken: ct);
        }
        
        //находит обработчик для указанного типа сценария
        private IScenario GetScenario(ScenarioType scenario)
        {
            var handler = _scenarios.FirstOrDefault(s => s.CanHandle(scenario));
            return handler ?? throw new KeyNotFoundException($"Сценарий {scenario} не найден");
        }
        //обработка сценария
        private async Task ProcessScenario(ITelegramBotClient botClient, ScenarioContext context, Update update, CancellationToken ct)
        {
            try
            {
                Console.WriteLine($"Идет обработка сценария: {context.CurrentScenario}, шаг: {context.CurrentStep}");
                var scenario = GetScenario(context.CurrentScenario);
                var result = await scenario.HandleMessageAsync(botClient, context, update, ct);

                if (result == ScenarioResult.Completed)
                {
                    await _contextRepository.ResetContext(context.UserId, ct);
                    var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
                    if (chatId.HasValue)
                    {
                        await botClient.SendMessage(chatId.Value, "Действие завершено", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                    }
                }
                else
                {
                    await _contextRepository.SetContext(context.UserId, context, ct);
                    if (context.CurrentStep != null)
                    {
                        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
                        if (chatId.HasValue)
                        {
                            await botClient.SendMessage(chatId.Value, "Вы можете отменить действие при помощи кнопки /cancel", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в сценарии: {context.CurrentScenario}: {ex}");
                await _contextRepository.ResetContext(context.UserId, ct);

                var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
                if (chatId.HasValue)
                {
                    await botClient.SendMessage(chatId.Value, $"Произошла ошибка: {ex.Message}", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                }
            }
        }
        //каллбек
        private async Task OnCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
        {
            if (callbackQuery.Message == null || callbackQuery.Data == null)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ошибка, данные отсутствуют", cancellationToken: ct);
                return;
            }
            if (callbackQuery.Data == "skip")
            {
                await HandleSkipCallback(botClient, callbackQuery, ct);
                return;
            }
            try
            {
                var user = await _userService.GetUser(callbackQuery.From.Id, ct);
                if (user == null)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Пожалуйста, зарегистрируйтесь с помощью /start", cancellationToken: ct);
                    return;
                }

                var context = await _contextRepository.GetContext(callbackQuery.From.Id, ct);
                // Обработка сценариев (если контекст активен)
                if (context != null)
                {
                    await ProcessScenario(botClient, context, new Update { CallbackQuery = callbackQuery }, ct);
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
                    return;
                }
                var callbackData = callbackQuery.Data;
                var separatorIndex = callbackData.IndexOf('|');
                var action = separatorIndex >= 0 ? callbackData.Substring(0, separatorIndex) : callbackData;
                switch (action)
                {
                    case "showtask":
                    case "completetask":
                        await HandleTaskAction(botClient, callbackQuery, user, action, ct);
                        break;
                    case "deletetask":
                        var deleteTaskContext = new ScenarioContext(callbackQuery.From.Id, ScenarioType.DeleteTask);
                        await _contextRepository.SetContext(deleteTaskContext.UserId, deleteTaskContext, ct);
                        await ProcessScenario(botClient, deleteTaskContext, new Update { CallbackQuery = callbackQuery }, ct);
                        break;
                    case "show":
                    case "show_completed":
                    case "addlist":
                    case "deletelist":
                        await HandleListAction(botClient, callbackQuery, user, action, ct);
                        break;
                    default:
                        await botClient.AnswerCallbackQuery(callbackQuery.Id, "Неизвестное действие: " + action, cancellationToken: ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, new Update { CallbackQuery = callbackQuery }, ct);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Произошла ошибка", cancellationToken: ct);
            }
        }

        private async Task HandleTaskAction(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, string action, CancellationToken ct)
        {
            var taskCallbackDto = ToDoItemCallbackDto.FromString(callbackQuery.Data);
            if (!taskCallbackDto.ToDoItemId.HasValue)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ошибка, ID задачи не указан", cancellationToken: ct);
                return;
            }
            switch (action)
            {
                case "showtask":
                    await HandleShowTaskCallback(botClient, callbackQuery, user, taskCallbackDto, ct);
                    break;
                case "completetask":
                    await HandleCompleteTaskCallback(botClient, callbackQuery, user, taskCallbackDto, ct);
                    break;
                case "deletetask":
                    await HandleDeleteTaskCallback(botClient, callbackQuery, user, taskCallbackDto, ct);
                    break;
            }
        }
        private async Task HandleListAction(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, string actionType, CancellationToken ct)
        {
            try
            {
                if (actionType == "show" || actionType == "show_completed")
                {
                    try
                    {
                        //пробуем распарсить как PagedListCallbackDto
                        var pagedDto = PagedListCallbackDto.FromString(callbackQuery.Data);
                        await HandleShowListCallback(botClient, callbackQuery, user, pagedDto, ct);
                        return;
                    }
                    catch (FormatException)
                    {
                        //если не получилось пробуем как ToDoListCallbackDto
                        try
                        {
                            var listDto = ToDoListCallbackDto.FromString(callbackQuery.Data);
                            var pagedDto = new PagedListCallbackDto
                            {
                                Action = listDto.Action,
                                ToDoListId = listDto.ToDoListId,
                                Page = 0
                            };
                            await HandleShowListCallback(botClient, callbackQuery, user, pagedDto, ct);
                            return;
                        }
                        catch (FormatException ex)
                        {
                            await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ошибка при обработке запроса", cancellationToken: ct);
                            return;
                        }
                    }
                }
                var listCallbackDto = ToDoListCallbackDto.FromString(callbackQuery.Data);
                switch (actionType)
                {
                    case "addlist":
                        await StartAddListScenario(botClient, callbackQuery, ct);
                        break;
                    case "deletelist":
                        await StartDeleteListScenario(botClient, callbackQuery, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleListAction: {ex}");
                await HandleException(ex, botClient, new Update { CallbackQuery = callbackQuery }, ct);
            }
        }
        private async Task HandleSkipCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
        {
            var context = await _contextRepository.GetContext(callbackQuery.From.Id, ct);
            if (callbackQuery.Data == "skip")
            {
                if (context?.CurrentStep == "Deadline")
                {
                    var fakeUpdate = new Update
                    {
                        Message = new Message
                        {
                            Text = "/skip",
                            Chat = callbackQuery.Message.Chat,
                            From = callbackQuery.From
                        }
                    };
                    await ProcessScenario(botClient, context, fakeUpdate, ct);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
            }
        }
        private async Task HandleShowTaskCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, ToDoItemCallbackDto dto, CancellationToken ct)
        {
            if (callbackQuery.Message == null)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ошибка: сообщение не найдено", cancellationToken: ct);
                return;
            }

            var task = await _toDoService.Get(dto.ToDoItemId.Value, ct);
            if (task == null || task.User?.UserId != user.UserId)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Задача не найдена", cancellationToken: ct);
                return;
            }

            var message = new StringBuilder();
            message.AppendLine($"📌 Задача: <b>{Helper.EscapeMarkdownV2(task.Name)}</b>");
            message.AppendLine($"📅 Создана: <b>{task.CreatedAt:dd.MM.yyyy HH:mm}</b>");
            if (task.State == ToDoItemState.Completed && task.StateChangedAt.HasValue)
            {
                message.AppendLine($"✅ Выполнена: <b>{task.StateChangedAt.Value:dd.MM.yyyy HH:mm}</b>");
            }
            else
            {
                message.AppendLine($"⏳ Статус: <b>{task.State}</b>");
            }
            if (task.Deadline.HasValue)
            {
                message.AppendLine($"⏰ Дедлайн: <b>{task.Deadline.Value:dd.MM.yyyy}</b>");
            }
            message.AppendLine($"🆔 ID: <pre>{Helper.EscapeMarkdownV2(task.Id.ToString())}</pre>");

            InlineKeyboardMarkup keyboard;
            if (task.State == ToDoItemState.Completed)
            {
                keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            Helper.BackToActiveButtonText,
                            new PagedListCallbackDto
                            {
                                Action = "show",
                                ToDoListId = task.List?.Id,
                                Page = 0
                            }.ToString())
                    }
                });
            }
            else
            {
                keyboard = Helper.GetTaskActionsKeyboard(task.Id);
            }
            try
            {
                await botClient.EditMessageText(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: message.ToString(),
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("Ошибка, сообщение не изменено"))
            {
            }
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
        }

        private async Task HandleCompleteTaskCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, ToDoItemCallbackDto dto, CancellationToken ct)
        {
            try
            {
                if (callbackQuery.Message == null)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Сообщение не найдено", cancellationToken: ct);
                    return;
                }
                if (!dto.ToDoItemId.HasValue)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "ID задачи не указан", cancellationToken: ct);
                    return;
                }

                var task = await _toDoService.Get(dto.ToDoItemId.Value, ct);
                if (task == null || task.User?.UserId != user.UserId)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Задача не найдена", cancellationToken: ct);
                    return;
                }
                await _toDoService.MarkCompleted(task.Id, user.UserId, ct);
                await botClient.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, $"✅ Задача выполнена: {Helper.EscapeMarkdownV2(task.Name)}", parseMode: ParseMode.Html, cancellationToken: ct);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Ошибка при выполнении задачи", cancellationToken: ct);
            }
        }

        private async Task HandleDeleteTaskCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, ToDoItemCallbackDto dto, CancellationToken ct)
        {
            try
            {
                if (callbackQuery.Message == null)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Сообщение не найдено", cancellationToken: ct);
                    return;
                }
                if (!dto.ToDoItemId.HasValue)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "ID задачи не указан", cancellationToken: ct);
                    return;
                }
                await _toDoService.Delete(dto.ToDoItemId.Value, ct);
                await botClient.DeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken: ct);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ Задача успешно удалена", cancellationToken: ct);
            }
            catch (TaskNotFoundException)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Задача не найдена", cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Ошибка при удалении задачи", cancellationToken: ct);
            }
        }
        private async Task HandleShowListCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, PagedListCallbackDto pagedDto, CancellationToken ct)
        {
            try
            {
                var allTasks = await _toDoService.GetByUserIdAndList(user.UserId, pagedDto.ToDoListId, ct);
                bool isCompletedView = pagedDto.Action == "show_completed";
                var filteredTasks = isCompletedView
                    ? allTasks.Where(t => t.State == ToDoItemState.Completed)
                             .OrderByDescending(t => t.StateChangedAt ?? t.CreatedAt)
                             .ToList()
                    : allTasks.Where(t => t.State == ToDoItemState.Active)
                             .OrderBy(t => t.CreatedAt)
                             .ToList();

                string listName = pagedDto.ToDoListId.HasValue
                    ? (await _toDoListService.Get(pagedDto.ToDoListId.Value, ct))?.Name ?? "Неизвестный список"
                    : "Без списка";

                if (isCompletedView && !filteredTasks.Any())
                {
                    await botClient.EditMessageText(callbackQuery.Message.Chat, callbackQuery.Message.MessageId, $"📋 Список: <b>{Helper.EscapeMarkdownV2(listName)}</b>\n ⚠️ Нет выполненных задач", parseMode: ParseMode.Html,
                        replyMarkup: new InlineKeyboardMarkup(
                            InlineKeyboardButton.WithCallbackData(
                                Helper.BackToActiveButtonText,
                                new PagedListCallbackDto
                                {
                                    Action = "show",
                                    ToDoListId = pagedDto.ToDoListId,
                                    Page = 0
                                }.ToString()
                            )
                        ),
                        cancellationToken: ct);
                    return;
                }

                var allTaskButtons = filteredTasks.Select(task =>
                    new KeyValuePair<string, string>(
                        $"{(pagedDto.Action == "show_completed" ? "✅ " : "")}{task.Name}",
                        new ToDoItemCallbackDto { Action = "showtask", ToDoItemId = task.Id }.ToString()
                    )).ToList();

                var keyboard = BuildPagedButtons(allTaskButtons, pagedDto);
                string messageText = $"📋 Список: <b>{Helper.EscapeMarkdownV2(listName)}</b>\n" +
                                   $"{(pagedDto.Action == "show_completed" ? "✅ Выполненные задачи" : "📌 Активные задачи")}\n" +
                                   $"Страница {pagedDto.Page + 1}";

                await botClient.EditMessageText(callbackQuery.Message.Chat, callbackQuery.Message.MessageId, messageText, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(callbackQuery.Message.Chat, "Произошла ошибка при загрузке списка задач", cancellationToken: ct);
            }
        }
        private InlineKeyboardMarkup BuildPagedButtons(IReadOnlyList<KeyValuePair<string, string>> callbackData, PagedListCallbackDto listDto)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            if (callbackData != null && callbackData.Count > 0)
            {
                var totalPages = (int)Math.Ceiling((double)callbackData.Count / _pageSize);
                listDto.Page = Math.Max(0, Math.Min(listDto.Page, totalPages - 1));
                buttons.AddRange(callbackData
                    .GetBatchByNumber(_pageSize, listDto.Page)
                    .Select(b => new[] { InlineKeyboardButton.WithCallbackData(b.Key, b.Value) }));

                var navigationButtons = new List<InlineKeyboardButton>();
                if (listDto.Page > 0)
                {
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData(
                        "⬅️",
                        new PagedListCallbackDto
                        {
                            Action = listDto.Action,
                            ToDoListId = listDto.ToDoListId,
                            Page = listDto.Page - 1
                        }.ToString()));
                }
                if (listDto.Page < totalPages - 1)
                {
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData(
                        "➡️",
                        new PagedListCallbackDto
                        {
                            Action = listDto.Action,
                            ToDoListId = listDto.ToDoListId,
                            Page = listDto.Page + 1
                        }.ToString()));
                }
                if (navigationButtons.Any())
                {
                    buttons.Add(navigationButtons.ToArray());
                }
            }

            buttons.Add(new[]
            {
            listDto.Action == "show_completed" ? InlineKeyboardButton.WithCallbackData(
                Helper.BackToActiveButtonText,
                new PagedListCallbackDto
                {
                    Action = "show",
                    ToDoListId = listDto.ToDoListId,
                    Page = 0
                }.ToString())
            : InlineKeyboardButton.WithCallbackData(
                Helper.ViewCompletedButtonText,
                new PagedListCallbackDto
                {
                    Action = "show_completed",
                    ToDoListId = listDto.ToDoListId,
                    Page = 0
                }.ToString())
            });
            return new InlineKeyboardMarkup(buttons);
        }
        
        private async Task StartAddListScenario(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
        {
            var context = new ScenarioContext(callbackQuery.From.Id, ScenarioType.AddList);
            await _contextRepository.SetContext(context.UserId, context, ct);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
            var fakeUpdate = new Update
            {
                Message = new Message
                {
                    Chat = callbackQuery.Message.Chat,
                    From = callbackQuery.From,
                    Text = string.Empty
                }
            };
            await ProcessScenario(botClient, context, fakeUpdate, ct);
        }
        private async Task StartDeleteListScenario(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
        {
            var context = new ScenarioContext(callbackQuery.From.Id, ScenarioType.DeleteList);
            await _contextRepository.SetContext(context.UserId, context, ct);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
            var fakeUpdate = new Update
            {
                Message = new Message
                {
                    Chat = callbackQuery.Message.Chat,
                    From = callbackQuery.From,
                    Text = string.Empty
                }
            };
            await ProcessScenario(botClient, context, fakeUpdate, ct);
        }

        //метод форматирования для задач
        private async Task<(string message, List<ToDoItem> tasks)> GetFormattedTasksList(Guid userId, Guid? listId, CancellationToken ct)
        {
            var tasks = await _toDoService.GetByUserIdAndList(userId, listId, ct);
            var activeTasks = tasks.Where(t => t.State == ToDoItemState.Active).OrderBy(t => t.CreatedAt).ToList();

            string message;
            if (!activeTasks.Any())
            {
                message = "Нет активных задач";
            }
            else
            {
                var tasksList = string.Join("\n",
                    activeTasks.Select((t, i) => $"{i + 1}. {t.Name} (ID: {t.Id})"));
                message = $"Задачи:\n{tasksList}";
            }

            return (message, activeTasks);
        }
        //кейсы исключений
        private async Task HandleException(Exception ex, ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

            if (!chatId.HasValue)
            {
                Console.WriteLine($"Не удалось определить chatId для отправки сообщения об ошибке. Ошибка: {ex.Message}");
                return;
            }

            var errorMessage = ex switch
            {
                ArgumentException argEx => $"Ошибка аргумента: {argEx.Message}",
                DuplicateTaskException taskDouble => $"Дубликат задачи: {taskDouble.Message}",
                TaskNotFoundException taskNotFound => $"Задача не найдена: {taskNotFound.Message}",
                UserNotFoundException userNotFound => $"Пользователь не найден: {userNotFound.Message}",
                _ => $"Произошла ошибка{(string.IsNullOrEmpty(ex.Message) ? "" : $": {ex.Message}")}"
            };

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "Произошла неизвестная ошибка";
            }

            await botClient.SendMessage(chatId.Value, errorMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}