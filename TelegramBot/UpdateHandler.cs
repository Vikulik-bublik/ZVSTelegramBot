using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ConsoleBot.Core.Entities;
using ConsoleBot.Core.Exceptions;
using ConsoleBot.Core.Services;
using System.Numerics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleBot.TelegramBot
{
    public delegate void MessageEventHandler(string message);
    public class UpdateHandler : IUpdateHandler
    {
        public event MessageEventHandler OnHandleUpdateStarted;
        public event MessageEventHandler OnHandleUpdateCompleted;
        private readonly IUserService _userService;
        private readonly IToDoService _toDoService;
        private readonly IToDoReportService _reportService;

        public UpdateHandler(IUserService userService, IToDoService toDoService, IToDoReportService reportService)
        {
            _userService = userService;
            _toDoService = toDoService;
            _reportService = reportService;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var telegramUserId = update.Message.From.Id;
            var telegramUserName = update.Message.From.Username;
            var command = update.Message.Text.Split(' ')[0];
            var messageText = update.Message.Text;
            OnHandleUpdateStarted?.Invoke(messageText);

            try
            {
                var user = await _userService.GetUser(telegramUserId, ct);
                await botClient.SendMessage(update.Message.Chat, $"Получил '{messageText}'", ct);
                //условия для задания максимального количества задач и ее максимальной длины
                if (user != null && user.WaitingForMaxTaskCount)
                {
                    await HandleMaxTaskCountInput(botClient, user, update, ct);
                    return;
                }

                if (user != null && user.WaitingForMaxLengthCount)
                {
                    await HandleMaxLengthCountInput(botClient, user, update, ct);
                    return;
                }
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
                OnHandleUpdateCompleted?.Invoke(messageText);
            }
        }
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"HandleError: {exception})"); //вот тут непоняла зачем в консоль выводить? Или эта информация не нужна пользователю?
            return Task.CompletedTask;
        }
        //метод для задания максимального количества задач при условии состояния
        private async Task HandleMaxTaskCountInput(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Trim();
            try
            {
                await Helper.SetMaxTaskCount(botClient, input, update, ct);
                user.WaitingForMaxTaskCount = false;
                await botClient.SendMessage(update.Message.Chat, "Теперь введите максимальную длину задачи (от 1 до 100):", ct);
                user.WaitingForMaxLengthCount = true;
            }
            catch (ArgumentException ex)
            {
                await botClient.SendMessage(update.Message.Chat, ex.Message, ct);
            }
        }
        //метод для задания максимальной длины задачи при условии состояния
        private async Task HandleMaxLengthCountInput(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Trim();
            try
            {
                await Helper.SetMaxLengthCount(botClient, input, update, ct);
                user.WaitingForMaxLengthCount = false;
                await botClient.SendMessage(update.Message.Chat, "Настройки успешно сохранены!", ct);
            }
            catch (ArgumentException ex)
            {
                await botClient.SendMessage(update.Message.Chat, ex.Message, ct);
            }
        }
        //метод обработки команды start
        private async Task Start(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            if (user == null)
            {
                user = await _userService.RegisterUser(update.Message.From.Id, update.Message.From.Username, ct);
                await botClient.SendMessage(update.Message.Chat, $"Добро пожаловать, Вы зарегистрированы как {user.TelegramUserName}!", ct);
                await botClient.SendMessage(update.Message.Chat, "Введите максимальное количество задач (от 1 до 100):", ct);
                user.WaitingForMaxTaskCount = true;
            }
            else if (user.WaitingForMaxTaskCount)
            {
                var input = update.Message.Text;
                await Helper.ValidateString(input,ct);
                try
                {
                    await Helper.SetMaxTaskCount(botClient, input, update, ct);
                    user.WaitingForMaxTaskCount = false;
                    await botClient.SendMessage(update.Message.Chat, "Теперь введите максимальную длину задачи (от 1 до 100):", ct);
                    user.WaitingForMaxLengthCount = true;
                }
                catch (ArgumentException ex)
                {
                    await botClient.SendMessage(update.Message.Chat, ex.Message, ct);
                }
            }
            else if (user.WaitingForMaxLengthCount)
            {
                var input = update.Message.Text;
                await Helper.ValidateString(input, ct);
                try
                {
                    await Helper.SetMaxLengthCount(botClient, input, update, ct);
                    user.WaitingForMaxLengthCount = false;
                    await botClient.SendMessage(update.Message.Chat, "Настройки успешно сохранены!", ct);
                }
                catch (ArgumentException ex)
                {
                    await botClient.SendMessage(update.Message.Chat, ex.Message, ct);
                }
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, $"Мы уже знакомы, {user.TelegramUserName}!", ct);
            }
        }

        //кейс обработки команд зарегистрированных пользователей
        private async Task HandleRegisteredUserCommands(ITelegramBotClient botClient, string command, ToDoUser? user, Update update, CancellationToken ct)
        {
            switch (command)
            {
                case "/info":
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 2.2.0 от 28.04.2025.", ct);
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
                    await botClient.SendMessage(update.Message.Chat, "Введена неверная команда. Пожалуйста, попробуйте снова.", ct);
                    break;
            }
        }

        //кейс обработки команд незарегистрированных пользователей
        private async Task HandleUnregisteredUserCommands(ITelegramBotClient botClient, string command, Update update, CancellationToken ct)
        {
            if (command == "/info" || command == "/help")
            {
                if (command == "/info")
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 2.2.0 от 28.04.2025.", ct);

                if (command == "/help")
                    await Help(botClient, update, ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Вы не зарегистрированы или введена неверная команда. " +
                    "\nНезарегистрированным пользователям доступны команды только /help и /info. \nПожалуйста, используйте команду /start для регистрации.", ct);
            }
        }

        //общий Хелп для всех категорий пользователей
        private async Task Help(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var helpMessage = "Доступные команды: /start, /help, /info. Для зарегистрированных пользователей:" +
                              "\n/addtask <имя задачи> - добавление в список новой задачи;" +
                              "\n/removetask <номер задачи> - удаление задачи из списка по ее порядковому номеру;" +
                              "\n/completetask <ID задачи> - установить задачу как выполненную по ее ID;" +
                              "\n/showtasks - показать все активные задачи;" +
                              "\n/showalltasks - показать весь список задач;" +
                              "\n/report - вывод статистики по задачам;" +
                              "\n/find <символы с которых начинается задача> - поиск задачи по нескольким сиволам ее начала.";

            await botClient.SendMessage(update.Message.Chat, helpMessage, ct);
        }

        //добавляем задачу по имени
        private async Task AddTask(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            var taskName = update.Message.Text.Substring(8).Trim();
            if (!string.IsNullOrWhiteSpace(taskName))
            {
                await _toDoService.Add(user, taskName, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача '{taskName}' добавлена.", ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Пожалуйста , укажите имя задачи.", ct);
            }
        }

        //удаляем задачу по ее порядковому номеру
        private async Task RemoveTask(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Substring(12).Trim();
            if (int.TryParse(input, out int taskIndex))
            {
                var tasks = await _toDoService.GetAllTasks(userId, ct);
                if (taskIndex >= 1 && taskIndex <= tasks.Count)
                {
                    var taskToRemove = tasks[taskIndex - 1];
                    await _toDoService.Delete(taskToRemove.Id, ct);
                    await botClient.SendMessage(update.Message.Chat, $"Задача '{taskToRemove.Name}' удалена.", ct);
                }
                else
                {
                    await botClient.SendMessage(update.Message.Chat, $"Задача с порядковым номером {taskIndex} не найдена. Пожалуйста, введите номер от 1 до {tasks.Count}.", ct);
                }
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Некорректный номер задачи.", ct);
            }
        }

        //показываем активные задачи
        private async Task ShowTasks(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            var tasks = await _toDoService.GetActiveByUserId(userId, ct);
            var taskList = string.Join(Environment.NewLine,
                tasks.Select(t => $"\nЗадача: {t.Name} - Время создания задачи: {t.CreatedAt} - ID задачи: {t.Id}"));

            await botClient.SendMessage(update.Message.Chat, string.IsNullOrEmpty(taskList) ? "Нет активных задач." : taskList, ct);
        }

        //меняем состаяние задачи из активного в неактивное по ID
        private async Task CompleteTask(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var telegramUserId = update.Message.From.Id;
            var user = await _userService.GetUser(telegramUserId, ct);
            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден.", ct);
                return;
            }
            if (Guid.TryParse(update.Message.Text.Substring(14).Trim(), out var taskId))
            {
                // Получаем задачу и проверяем принадлежность пользователю
                var taskUser = await _toDoService.GetAllTasks(user.UserId, ct);
                var task = taskUser.FirstOrDefault(t => t.Id == taskId);

                if (task == null)
                {
                    await botClient.SendMessage(update.Message.Chat, "Задача не найдена.", ct);
                    return;
                }

                await _toDoService.MarkCompleted(taskId, user.UserId, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача '{task.Name}' помечена как выполненная.", ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Некорректный формат ID задачи.", ct);
            }
        }

        //показываем все активные и неактивные задачи
        private async Task ShowAllTasks(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var telegramUserId = update.Message.From.Id;
            var user = await _userService.GetUser(telegramUserId, ct);

            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден.", ct);
                return;
            }

            var allTasks = await _toDoService.GetAllTasks(user.UserId, ct);
            var allTaskList = string.Join(Environment.NewLine,
                allTasks.Select(t => $"\nСтатус задачи: ({t.State}) Задача: {t.Name} - Время создания задачи: {t.CreatedAt} - ID задачи: {t.Id}"));

            await botClient.SendMessage(update.Message.Chat, string.IsNullOrEmpty(allTaskList) ? "Нет задач." : allTaskList, ct);
        }

        //вывод статистики по задачам
        private async Task Report(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var (total, completed, active, generatedAt) = await _reportService.GetUserStats(user.UserId, ct);
            var report = $"Статистика по задачам на {generatedAt}" +
                $"\nВсего: {total}; Завершенных: {completed}; Активных: {active};";

            await botClient.SendMessage(update.Message.Chat, report, ct);
        }

        //поиск задачи по префиксу
        private async Task Find(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var namePrefix = update.Message.Text.Substring(6).Trim();
            if (string.IsNullOrWhiteSpace(namePrefix))
            {
                await botClient.SendMessage(update.Message.Chat, "Для поиска задачи введите ключевое слово после команды /find", ct);
                return;
            }

            var tasks = await _toDoService.Find(user, namePrefix, ct);
            var taskList = string.Join(Environment.NewLine,
                tasks.Select(t => $"\nЗадача: {t.Name} - Время создания задачи: {t.CreatedAt} - ID задачи: {t.Id}"));

            await botClient.SendMessage(update.Message.Chat,
                string.IsNullOrEmpty(taskList)
                    ? $"Не найдено задач, начинающихся с '{namePrefix}'"
                    : $"По вашему запросу найдены следующие задачи:\n{taskList}", ct);
        }
        
        //кейсы исключений
        private async Task HandleException(Exception ex, ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            switch (ex)
            {
                case ArgumentException argEx:
                    await botClient.SendMessage(update.Message.Chat, $"Ошибка аргумента: {argEx.Message}", ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла ошибка. Пожалуйста , проверьте введенные данные.", ct);
                    break;

                case TaskCountLimitException taskCountLimit:
                    await botClient.SendMessage(update.Message.Chat, $"Превышен лимит: {taskCountLimit.Message}", ct);
                    break;

                case TaskLengthLimitException taskLengthLimit:
                    await botClient.SendMessage(update.Message.Chat, $"Превышен лимит: {taskLengthLimit.Message}", ct);
                    break;

                case DuplicateTaskException taskDouble:
                    await botClient.SendMessage(update.Message.Chat, $"Дубликат задачи: {taskDouble.Message}", ct);
                    break;

                default:
                    await botClient.SendMessage(update.Message.Chat, $"Неизвестная ошибка: {ex.Message}", ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла неизвестная ошибка. Пожалуйста , попробуйте позже.", ct);
                    break;
            }
        }
    }
}

