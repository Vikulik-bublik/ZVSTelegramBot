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

        public UpdateHandler(IUserService userService, IToDoService toDoService, IToDoReportService reportService, IEnumerable<IScenario> scenarios, IScenarioContextRepository contextRepository)
        {
            _userService = userService;
            _toDoService = toDoService;
            _reportService = reportService;
            _scenarios = scenarios;
            _contextRepository = contextRepository;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            //чтобы избегать краша при отправке не текста
            if (update.Message?.Text == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пожалуйста, используйте текстовые сообщения", cancellationToken: ct);
                return;
            }
            var telegramUserId = update.Message.From.Id;
            OnHandleUpdateStarted?.Invoke(update.Message.Text);
            
            try
            {
                //условие обработки команды cancel
                if (update.Message.Text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    var resetcontext = await _contextRepository.GetContext(telegramUserId, ct);
                    if (resetcontext != null)
                    {
                        await _contextRepository.ResetContext(telegramUserId, ct);
                        await botClient.SendMessage(update.Message.Chat, "Текущее действие отменено", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
                        return;
                    }
                }
                var context = await _contextRepository.GetContext(telegramUserId, ct);
                if (context != null)
                {
                    await ProcessScenario(botClient, context, update, ct);
                    return;
                }

                var user = await _userService.GetUser(telegramUserId, ct);
                var command = update.Message.Text.Split(' ')[0];
                //условие обработки команды start
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
                OnHandleUpdateCompleted?.Invoke(update.Message.Text);
            }
        }
        //нужен ли этот метод???? это вроде из старых ДЗ
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken ct)
        {
            Console.WriteLine($"HandleError: {exception.Message})");
            return Task.CompletedTask;
        }
        
        //метод обработки команды start
        private async Task Start(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            try
            {
                if (user == null)
                {
                    // Регистрация нового пользователя
                    user = await _userService.RegisterUser(update.Message.From.Id, update.Message.From.Username, ct);
                    await botClient.SendMessage(update.Message.Chat, $"Добро пожаловать, Вы зарегистрированы как {user.TelegramUserName}!", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
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
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.1.1 от 16.06.2025", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
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
                case "/showtasks":
                    await ShowTasks(botClient, user.UserId, update, ct);
                    break;
                case "/completetask":
                    await CompleteTask(botClient, update,ct);
                    break;
                case "/showalltasks":
                    await ShowAllTasks(botClient, update, ct);
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
        //кейс обработки команд незарегистрированных пользователей
        private async Task HandleUnregisteredUserCommands(ITelegramBotClient botClient, string command, Update update, CancellationToken ct)
        {
            if (command == "/info" || command == "/help")
            {
                if (command == "/info")
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.1.1 от 16.06.2025" +
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
                              "\n/showtasks - показать все активные задачи" +
                              "\n/showalltasks - показать весь список задач" +
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
                await botClient.SendMessage(update.Message.Chat, helpMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken:    ct);
            }
        }
        //добавляем задачу по имени
        private async Task AddTask(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            var context = new ScenarioContext(update.Message.From.Id, ScenarioType.AddTask);
            await _contextRepository.SetContext(context.UserId, context, ct);
            await ProcessScenario(botClient, context, update, ct);
        }
        //удаляем задачу по ее порядковому номеру
        private async Task RemoveTask(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Substring(11).Trim();
            if (!int.TryParse(input, out int taskNumber) || taskNumber < 1)
            {
                await botClient.SendMessage(update.Message.Chat, "После команды необходимо указать порядковый номер задачи, начиная с 1", cancellationToken: ct);
                return;
            }
            var allTasks = (await _toDoService.GetActiveByUserId(userId, ct))
                .OrderBy(t => t.CreatedAt).
                ToList();

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
            await botClient.SendMessage(update.Message.Chat, $"Задача `{Helper.EscapeMarkdownV2(taskToRemove.Name)}` успешно удалена", replyMarkup: Helper.GetAuthorizedKeyboard(),  parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
        }
        //показываем активные задачи
        private async Task ShowTasks(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;
            var tasks = (await _toDoService.GetActiveByUserId(userId, ct))
            .OrderBy(t => t.CreatedAt)
            .ToList();
            var taskList = string.Join(Environment.NewLine, tasks.Select((t, index) =>
                $"\nЗадача *{index + 1}*: `{Helper.EscapeMarkdownV2(t.Name)}`" +
                $"\nВремя создания задачи: {Helper.EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
                $"\nID задачи: `{Helper.EscapeMarkdownV2(t.Id.ToString())}`"
            ));
            var finalMessage = string.IsNullOrEmpty(taskList) ? "Нет активных задач" : $"Активные задачи:{taskList}";
            await botClient.SendMessage(update.Message.Chat, finalMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
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
                // Получаем задачу и проверяем принадлежность пользователю
                var taskUser = await _toDoService.GetAllTasks(user.UserId, ct);
                var task = taskUser.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                {
                    await botClient.SendMessage(update.Message.Chat, "Задача не найдена", cancellationToken: ct);
                    return;
                }
                await _toDoService.MarkCompleted(taskId, user.UserId, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача `{Helper.EscapeMarkdownV2(task.Name)}` помечена как выполненная", replyMarkup: Helper.GetAuthorizedKeyboard(), parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "После команды необходимо указать корректный ID задачи", cancellationToken: ct);
            }
        }
        //показываем все активные и завершенные задачи
        private async Task ShowAllTasks(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;
            var telegramUserId = update.Message.From.Id;
            var user = await _userService.GetUser(telegramUserId, ct);
            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден", cancellationToken: ct);
                return;
            }
            var allTasks = (await _toDoService.GetAllTasks(user.UserId, ct))
                .OrderBy(t => t.CreatedAt)
                .ToList();
            var allTaskList = string.Join(Environment.NewLine, allTasks.Select((t, index) =>
               $"\nЗадача *{index + 1}*: `{Helper.EscapeMarkdownV2(t.Name)}`, статус: {Helper.EscapeMarkdownV2(t.State.ToString())}" +
               $"\nВремя создания задачи: {Helper.EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
               $"\nID задачи: `{Helper.EscapeMarkdownV2(t.Id.ToString())}`"
           ));
            var finalMessage = string.IsNullOrEmpty(allTaskList) ? "Нет задач" : $"Все задачи:{allTaskList}";
            await botClient.SendMessage(update.Message.Chat, finalMessage, replyMarkup: Helper.GetAuthorizedKeyboard(), parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
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
                $"\nЗадача: `{Helper.EscapeMarkdownV2(t.Name)}`, статус: {Helper.   EscapeMarkdownV2(t.State.ToString())}" +
                $"\nВремя создания задачи: {Helper.EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
                $"\nID задачи: `{Helper.EscapeMarkdownV2(t.Id.ToString())}`"
            ));
            await botClient.SendMessage(update.Message.Chat,
                string.IsNullOrEmpty(taskList)
                    ? $"Не найдено задач, начинающихся с '{namePrefix}'"
                    : $"По вашему запросу найдены следующие задачи:\n{taskList}", replyMarkup: Helper.GetAuthorizedKeyboard(),parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
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
            var scenario = GetScenario(context.CurrentScenario);
            var result = await scenario.HandleMessageAsync(botClient, context, update, ct);

            if (result == ScenarioResult.Completed)
            {
                await _contextRepository.ResetContext(context.UserId, ct);
                await botClient.SendMessage(update.Message.Chat, "Действие завершено", replyMarkup: Helper.GetAuthorizedKeyboard(), cancellationToken: ct);
            }
            else
            {
                await _contextRepository.SetContext(context.UserId, context, ct);
                if (context.CurrentStep != null)
                {
                    await botClient.SendMessage(update.Message.Chat, "Вы можете отменить действие при помощи кнопки /cancel", replyMarkup: Helper.GetCancelKeyboard(), cancellationToken: ct);
                }
            }
        }
        //кейсы исключений
        private async Task HandleException(Exception ex, ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            switch (ex)
            {
                case ArgumentException argEx:
                    await botClient.SendMessage(update.Message.Chat, $"Ошибка аргумента: {argEx.Message}", cancellationToken: ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла ошибка. Пожалуйста, проверьте введенные данные", cancellationToken: ct);
                    break;

                case DuplicateTaskException taskDouble:
                    await botClient.SendMessage(update.Message.Chat, $"Дубликат задачи: {taskDouble.Message}", cancellationToken: ct);
                    break;

                default:
                    await botClient.SendMessage(update.Message.Chat, $"Неизвестная ошибка: {ex.Message}", cancellationToken: ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла неизвестная ошибка. Пожалуйста, попробуйте позже", cancellationToken: ct);
                    break;
            }
        }
    }
}

