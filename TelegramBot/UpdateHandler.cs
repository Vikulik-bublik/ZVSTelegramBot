﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Numerics;
using System.Diagnostics;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Exceptions;
using ZVSTelegramBot.Core.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
                await botClient.SendMessage(update.Message.Chat, $"Получил '{messageText}'", cancellationToken: ct);
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
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken ct)
        {
            Console.WriteLine($"HandleError: {exception.Message})");
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
                await botClient.SendMessage(update.Message.Chat, "Теперь введите максимальную длину задачи (от 1 до 100):", cancellationToken: ct);
                user.WaitingForMaxLengthCount = true;
            }
            catch (ArgumentException ex)
            {
                await botClient.SendMessage(update.Message.Chat, ex.Message, cancellationToken: ct);
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
                await botClient.SendMessage(update.Message.Chat, "Настройки успешно сохранены!", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
            }
            catch (ArgumentException ex)
            {
                await botClient.SendMessage(update.Message.Chat, ex.Message, cancellationToken: ct);
            }
        }
        //метод создания кнопки команды start для незарегистрированных
        private ReplyKeyboardMarkup GetUnauthorizedKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
            new KeyboardButton("/start")
        })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }
        //метод создания кнопок команд showtasks, showalltasks, report для зарегистрированных
        private ReplyKeyboardMarkup GetAuthorizedKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
            new[] { new KeyboardButton("/showtasks"), new KeyboardButton("/showalltasks"), new KeyboardButton("/report") },
        })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }
        //метод обработки команды start
        private async Task Start(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            if (user == null)
            {
                user = await _userService.RegisterUser(update.Message.From.Id, update.Message.From.Username, ct);
                await botClient.SendMessage(update.Message.Chat, $"Добро пожаловать, Вы зарегистрированы как {user.TelegramUserName}!", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
                await botClient.SendMessage(update.Message.Chat, "Введите максимальное количество задач (от 1 до 100):", cancellationToken: ct);
                user.WaitingForMaxTaskCount = true;
            }
            else if (user.WaitingForMaxTaskCount)
            {
                await HandleMaxTaskCountInput(botClient, user, update, ct);
            }
            else if (user.WaitingForMaxLengthCount)
            {
                await HandleMaxLengthCountInput(botClient, user, update, ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, $"Мы уже знакомы, {user.TelegramUserName}!", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
            }
        }
        //кейсы обработки команд зарегистрированных пользователей
        private async Task HandleRegisteredUserCommands(ITelegramBotClient botClient, string command, ToDoUser? user, Update update, CancellationToken ct)
        {
            switch (command)
            {
                case "/info":
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.0.0 от 06.05.2025.", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
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
                    await botClient.SendMessage(update.Message.Chat, "Введена неверная команда. Пожалуйста, попробуйте снова.", cancellationToken: ct);
                    break;
            }
        }

        //кейс обработки команд незарегистрированных пользователей
        private async Task HandleUnregisteredUserCommands(ITelegramBotClient botClient, string command, Update update, CancellationToken ct)
        {
            if (command == "/info" || command == "/help")
            {
                if (command == "/info")
                    await botClient.SendMessage(update.Message.Chat, "Вот информация о боте. \nДата создания: 23.02.2025. Версия: 3.0.0 от 06.05.2025." +
                        "\nПожалуйста, зарегистрируйтесь, нажав кнопку /start", replyMarkup: GetUnauthorizedKeyboard(), cancellationToken: ct);

                if (command == "/help")
                    await Help(botClient, update, ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Вы не зарегистрированы." +
                    "\nНезарегистрированным пользователям доступны команды только /help и /info. \nПожалуйста, используйте команду /start для регистрации.", replyMarkup: GetUnauthorizedKeyboard(), cancellationToken: ct);
            }
        }

        //общий Хелп для всех категорий пользователей. Однако добавлено различие в кнопках
        private async Task Help(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUser(update.Message.From.Id, ct);
            bool isRegistered = user != null;

            var helpMessage = "Доступные команды:" +
                              "\n/start, /help, /info" +
                              "\nДля зарегистрированных пользователей доступны:" +
                              "\n/addtask <имя задачи> - добавление в список новой задачи;" +
                              "\n/removetask <номер задачи> - удаление задачи из списка по ее порядковому номеру;" +
                              "\n/completetask <ID задачи> - установить задачу как выполненную по ее ID;" +
                              "\n/showtasks - показать все активные задачи;" +
                              "\n/showalltasks - показать весь список задач;" +
                              "\n/report - вывод статистики по задачам;" +
                              "\n/find <префикс> - поиск задачи по нескольким сиволам ее начала.";
            if (!isRegistered)
            {
                await botClient.SendMessage(update.Message.Chat, $"{helpMessage}" +
                    $"\nПожалуйста, зарегистрируйтесь, нажав кнопку /start", replyMarkup: GetUnauthorizedKeyboard(),cancellationToken: ct);
            }
            else
            {
              await botClient.SendMessage(update.Message.Chat, helpMessage, replyMarkup: GetAuthorizedKeyboard(), cancellationToken:    ct);
            }
        }

        //добавляем задачу по имени
        private async Task AddTask(ITelegramBotClient botClient, ToDoUser? user, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;

            var taskName = update.Message.Text.Substring(8).Trim();
            if (!string.IsNullOrWhiteSpace(taskName))
            {
                await _toDoService.Add(user, taskName, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача '{taskName}' добавлена.", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Внимание!  После команды необходимо указать имя задачи.", cancellationToken: ct);
            }
        }

        //удаляем задачу по ее порядковому номеру
        private async Task RemoveTask(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            var input = update.Message.Text.Substring(11).Trim();
            if (int.TryParse(input, out int taskIndex))
            {
                var tasks = await _toDoService.GetAllTasks(userId, ct);
                if (taskIndex >= 1 && taskIndex <= tasks.Count)
                {
                    var taskToRemove = tasks[taskIndex - 1];
                    await _toDoService.Delete(taskToRemove.Id, ct);
                    await botClient.SendMessage(update.Message.Chat, $"Задача `{EscapeMarkdownV2(taskToRemove.Name)}` удалена", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct, parseMode: ParseMode.MarkdownV2);
                }
                else
                {
                    await botClient.SendMessage(update.Message.Chat, $"Задача с порядковым номером {taskIndex} не найдена. Пожалуйста, введите номер от 1 до {tasks.Count}.", cancellationToken: ct);
                }
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "Внимание!  После команды необходимо указать порядковый номер задачи, либо введен некорректный номер задачи.", cancellationToken: ct);
            }
        }

        //показываем активные задачи
        private async Task ShowTasks(ITelegramBotClient botClient, Guid userId, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;

            var tasks = await _toDoService.GetActiveByUserId(userId, ct);
            var taskList = string.Join(Environment.NewLine, tasks.Select(t =>
                $"\nЗадача: {EscapeMarkdownV2(t.Name)}" +
                $"\nВремя создания задачи: {EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
                $"\nID задачи: `{EscapeMarkdownV2(t.Id.ToString())}`"
            ));

            var finalMessage = string.IsNullOrEmpty(taskList) ? "Нет активных задач" : $"Активные задачи:{taskList}";

            await botClient.SendMessage(
                update.Message.Chat,
                finalMessage,
                replyMarkup: GetAuthorizedKeyboard(),
                cancellationToken: ct,
                parseMode: ParseMode.MarkdownV2
            );
        }

        //меняем состаяние задачи из активного в неактивное по ID
        private async Task CompleteTask(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUser(update.Message.From.Id, ct);
            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден.", cancellationToken: ct);
                return;
            }
            if (Guid.TryParse(update.Message.Text.Substring(13).Trim(), out var taskId))
            {
                // Получаем задачу и проверяем принадлежность пользователю
                var taskUser = await _toDoService.GetAllTasks(user.UserId, ct);
                var task = taskUser.FirstOrDefault(t => t.Id == taskId);

                if (task == null)
                {
                    await botClient.SendMessage(update.Message.Chat, "Задача не найдена.", cancellationToken: ct);
                    return;
                }
                
                await _toDoService.MarkCompleted(taskId, user.UserId, ct);
                await botClient.SendMessage(update.Message.Chat, $"Задача '{task.Name}' помечена как выполненная.", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(update.Message.Chat, "После команды необходимо указать корректный ID задачи.", cancellationToken: ct);
            }
        }

        //показываем все активные и неактивные задачи
        private async Task ShowAllTasks(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;

            var telegramUserId = update.Message.From.Id;
            var user = await _userService.GetUser(telegramUserId, ct);

            if (user == null)
            {
                await botClient.SendMessage(update.Message.Chat, "Пользователь не найден.", cancellationToken: ct);
                return;
            }

            var allTasks = await _toDoService.GetAllTasks(user.UserId, ct);
            var allTaskList = string.Join(Environment.NewLine, allTasks.Select(t =>
               $"\nЗадача: {EscapeMarkdownV2(t.Name)}, статус: {EscapeMarkdownV2(t.State.ToString())}" +
               $"\nВремя создания задачи: {EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
               $"\nID задачи: `{EscapeMarkdownV2(t.Id.ToString())}`"
           ));
            
            var finalMessage = string.IsNullOrEmpty(allTaskList) ? "Нет задач" : $"Все задачи:{allTaskList}";
            await botClient.SendMessage(
                update.Message.Chat,
                finalMessage,
                replyMarkup: GetAuthorizedKeyboard(),
                cancellationToken: ct,
                parseMode: ParseMode.MarkdownV2
            );
        }

        //вывод статистики по задачам
        private async Task Report(ITelegramBotClient botClient, ToDoUser user, Update update, CancellationToken ct)
        {
            var (total, completed, active, generatedAt) = await _reportService.GetUserStats(user.UserId, ct);
            var report = $"Статистика по задачам на {generatedAt}" +
                $"\nВсего: {total}; Завершенных: {completed}; Активных: {active}";

            await botClient.SendMessage(update.Message.Chat, report, replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct);
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
                $"\nЗадача: {EscapeMarkdownV2(t.Name)}" +
                $"\nВремя создания задачи: {EscapeMarkdownV2(t.CreatedAt.ToString("dd:MM:yyyy HH:mm:ss"))}" +
                $"\nID задачи: `{EscapeMarkdownV2(t.Id.ToString())}`"
            ));

            await botClient.SendMessage(update.Message.Chat,
                string.IsNullOrEmpty(taskList)
                    ? $"Не найдено задач, начинающихся с '{namePrefix}'"
                    : $"По вашему запросу найдены следующие задачи:\n{taskList}", replyMarkup: GetAuthorizedKeyboard(), cancellationToken: ct, parseMode: ParseMode.MarkdownV2);
        }

        //экранируем спецсимволы, иначе вылетает ошибка у Бота
        private string EscapeMarkdownV2(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Replace("_", "\\_")
                       .Replace("*", "\\*")
                       .Replace("[", "\\[")
                       .Replace("]", "\\]")
                       .Replace("(", "\\(")
                       .Replace(")", "\\)")
                       .Replace("~", "\\~")
                       .Replace("`", "\\`")
                       .Replace(">", "\\>")
                       .Replace("#", "\\#")
                       .Replace("+", "\\+")
                       //.Replace("-", "\\-")
                       .Replace("=", "\\=")
                       .Replace("|", "\\|")
                       .Replace("{", "\\{")
                       .Replace("}", "\\}")
                       .Replace("!", "\\!")
                       .Replace(".", "\\.")
                       .Replace("\\", "\\\\");
        }
        //кейсы исключений
        private async Task HandleException(Exception ex, ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            switch (ex)
            {
                case ArgumentException argEx:
                    await botClient.SendMessage(update.Message.Chat, $"Ошибка аргумента: {argEx.Message}", cancellationToken: ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла ошибка. Пожалуйста, проверьте введенные данные.", cancellationToken: ct);
                    break;

                case TaskCountLimitException taskCountLimit:
                    await botClient.SendMessage(update.Message.Chat, $"Превышен лимит: {taskCountLimit.Message}", cancellationToken: ct);
                    break;

                case TaskLengthLimitException taskLengthLimit:
                    await botClient.SendMessage(update.Message.Chat, $"Превышен лимит: {taskLengthLimit.Message}", cancellationToken: ct);
                    break;

                case DuplicateTaskException taskDouble:
                    await botClient.SendMessage(update.Message.Chat, $"Дубликат задачи: {taskDouble.Message}", cancellationToken: ct);
                    break;

                default:
                    await botClient.SendMessage(update.Message.Chat, $"Неизвестная ошибка: {ex.Message}", cancellationToken: ct);
                    await botClient.SendMessage(update.Message.Chat, "Произошла неизвестная ошибка. Пожалуйста, попробуйте позже.", cancellationToken: ct);
                    break;
            }
        }
    }
}

