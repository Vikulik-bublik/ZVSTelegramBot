using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Exceptions;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.DTO;
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
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.2.0 от 05.07.2025", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                    break;
                case "/help":
                    await Help(botClient, update, ct);
                    break;
                case "/addtask":
                    await AddTask(botClient, user, update,ct);
                    break;
                case "/removetask":
                    await RemoveTask(botClient, user.UserId, update, ct);
                    break;
                case "/show":
                    await Show(botClient, user.UserId, update, ct);
                    break;
                case "/completetask":
                    await CompleteTask(botClient, update,ct);
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
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.2.0 от 05.07.2025" +
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
                              "\n/removetask <номер задачи> - удаление задачи из списка по ее порядковому номеру" +
                              "\n/completetask <ID задачи> - установить задачу как выполненную по ее ID" +
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
        //удаляем задачу по ее порядковому номеру, тут вот вопрос как удалять в списках? пока не меняла метод
        private async Task RemoveTask(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Substring(11).Trim();
            if (!int.TryParse(input, out int taskNumber) || taskNumber < 1)
            {
                await botClient.SendMessage(update.Message.Chat, "После команды необходимо указать порядковый номер задачи, начиная с 1", cancellationToken: ct);
                return;
            }
            var allTasks = (await _toDoService.GetActiveByUserId(userId, ct)).OrderBy(t => t.CreatedAt).ToList();

            if (taskNumber > allTasks.Count)
            {
                var message = allTasks.Count == 0
                    ? "У вас нет задач для удаления"
                    : $"Номер задачи должен быть от 1 до {allTasks.Count}";
                await botClient.SendMessage(update.Message.Chat, message, cancellationToken: ct);
                return;
            }
            var taskToRemove = allTasks[taskNumber - 1];
            await _toDoService.Delete(taskToRemove.Id, ct);
            await botClient.SendMessage(update.Message.Chat, $"Задача <b>{Helper.EscapeMarkdownV2(taskToRemove.Name)}</b> успешно удалена", replyMarkup: Helper.GetAuthorizedKeyboard(),  parseMode: ParseMode.Html, cancellationToken: ct);
        }
        //показываем активные списки
        private async Task Show(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            try
            {
                var lists = await _toDoListService.GetUserLists(userId, ct);
                var (_, tasksWithoutList) = await GetFormattedTasksList(userId, null, ct);

                var keyboard = Helper.GetListSelectionKeyboard((List<ToDoList>)lists, tasksWithoutList);

                await botClient.SendMessage(update.Message.Chat, "Выберите список:", replyMarkup: keyboard, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, update, ct);
            }
        }

        //меняем состаяние задачи из активного в неактивное по ID
        private async Task CompleteTask(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUser(update.Message.From.Id, ct);
            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден", cancellationToken: ct);
                return;
            }
            if (Guid.TryParse(update.Message.Text.Substring(13).Trim(), out var taskId))
            {
                var taskUser = await _toDoService.GetAllTasks(user.UserId, ct);
                var task = taskUser.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                {
                    await botClient.SendMessage(update.Message.Chat, "Задача не найдена", cancellationToken: ct);
                    return;
                }
                await _toDoService.MarkCompleted(taskId, user.UserId, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача <b>{Helper.EscapeMarkdownV2(task.Name)}</b> помечена как выполненная", replyMarkup: Helper.GetAuthorizedKeyboard(), parseMode: ParseMode.Html, cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "После команды необходимо указать корректный ID задачи", cancellationToken: ct);
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
            try
            {
                if (callbackQuery.Message == null || callbackQuery.Data == null)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ошибка: данные отсутствуют", cancellationToken: ct);
                    return;
                }

                var user = await _userService.GetUser(callbackQuery.From.Id, ct);
                if (user == null)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Пожалуйста, зарегистрируйтесь с помощью /start", cancellationToken: ct);
                    return;
                }

                var context = await _contextRepository.GetContext(callbackQuery.From.Id, ct);
                //обработка кнопки Пропустить
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
                    return;
                }
                if (context != null)
                {
                    await ProcessScenario(botClient, context, new Update { CallbackQuery = callbackQuery }, ct);
                    return;
                }

                var callbackDto = ToDoListCallbackDto.FromString(callbackQuery.Data);
                switch (callbackDto)
                {
                    case ToDoListCallbackDto { Action: "show" } listCallback:
                        await HandleShowListCallback(botClient, callbackQuery, user, listCallback, ct);
                        break;
                    case { Action: "addlist" }:
                        await StartAddListScenario(botClient, callbackQuery, ct);
                        break;
                    case { Action: "deletelist" }:
                        await StartDeleteListScenario(botClient, callbackQuery, ct);
                        break;
                    default:
                        await botClient.AnswerCallbackQuery(callbackQuery.Id, "Неизвестное действие", cancellationToken: ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                await HandleException(ex, botClient, new Update { CallbackQuery = callbackQuery }, ct);
            }
        }

        private async Task HandleShowListCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery, ToDoUser user, ToDoListCallbackDto dto, CancellationToken ct)
        {
            //получение задач для указанного списка или без списка
            var tasks = await _toDoService.GetByUserIdAndList(user.UserId, dto.ToDoListId, ct);
            var activeTasks = tasks.Where(t => t.State == ToDoItemState.Active).OrderBy(t => t.CreatedAt).ToList();
            string listName = dto.ToDoListId.HasValue
            ? (await _toDoListService.Get(dto.ToDoListId.Value, ct))?.Name ?? "Неизвестный список" : "Без списка";
            var message = new StringBuilder();
            message.AppendLine($"📋 Список: <b>{Helper.EscapeMarkdownV2(listName)}</b>");

            if (activeTasks.Any())
            {
                message.AppendLine("📌 Активные задачи:");
                foreach (var (task, index) in activeTasks.Select((t, i) => (t, i + 1)))
                {
                    message.AppendLine($"{index}. <b>{task.Name}</b>");
                    message.AppendLine(task.Deadline.HasValue
                        ? $"⏰ Дедлайн: <b>{task.Deadline.Value:dd.MM.yyyy}</b>"
                        : "⏰ Дедлайн: <b>Не установлен</b>");
                    message.AppendLine($"🆔 ID: <pre>{task.Id}</pre>");
                    message.AppendLine();                }
            }
            else
            {
                message.AppendLine("Нет активных задач в этом списке");
            }
            await botClient.SendMessage(callbackQuery.Message.Chat, message.ToString(), parseMode: ParseMode.Html, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
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
            var errorMessage = ex switch
            {
                ArgumentException argEx => $"Ошибка аргумента: {argEx.Message}",
                DuplicateTaskException taskDouble => $"Дубликат задачи: {taskDouble.Message}",
                TaskNotFoundException taskNotFound => $"Задача не найдена: {taskNotFound.Message}",
                UserNotFoundException userNotFound => $"Пользователь не найден: {userNotFound.Message}",
                _ => $"Неизвестная ошибка: {ex.Message}"
            };

            await botClient.SendMessage(update.Message.Chat, errorMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
        }
    }
}