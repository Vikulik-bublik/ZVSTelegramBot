using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.Infrastructure.DataAccess;
using ZVSTelegramBot.TelegramBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


namespace ZVSTelegramBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var usersStoragePath = Path.Combine(Environment.CurrentDirectory, "Users");
            var userRepository = new FileUserRepository(usersStoragePath);
            var storagePath = Path.Combine(Environment.CurrentDirectory, "ToDoItems");
            var toDoRepository = new FileToDoRepository(storagePath);
            var reportService = new ToDoReportService(toDoRepository);
            var userService = new UserService(userRepository);
            var toDoService = new ToDoService(toDoRepository);
            var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = [UpdateType.Message],
                    DropPendingUpdates = true
                };
            var handler = new UpdateHandler(userService, toDoService, reportService);
            string token = Environment.GetEnvironmentVariable("TELEGRAM_CsharpBOT_TOKEN", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Токен бота не найден.");
                return;
            }
            var botClient = new TelegramBotClient(token);
            using var cts = new CancellationTokenSource();
            //меню команд
            var commands = new List<BotCommand>
            {
                new() { Command = "start", Description = "Начать работу с ботом/заново установить лимиты для задач" },
                new() { Command = "addtask", Description = "Добавление задачи" },
                new() { Command = "removetask", Description = "Удаление задачи" },
                new() { Command = "completetask", Description = "Отметить задачу выполненной" },
                new() { Command = "showtasks", Description = "Показать активные задачи" },
                new() { Command = "showalltasks", Description = "Показать все задачи" },
                new() { Command = "report", Description = "Статистика по задачам" },
                new() { Command = "find", Description = "Поиск задачи по префиксу" },
                new() { Command = "help", Description = "Помощь по командам" },
                new() { Command = "info", Description = "Информация о боте" }
            };
            try
            {
                await botClient.SetMyCommands(commands, cancellationToken: cts.Token);
                //потуги сделать отдельную клавиатуру для дополняемых команд
                //var commandKeyboard = new ReplyKeyboardMarkup(new[]
                //{
                //    new[]
                //    {
                //        new KeyboardButton("/addtask"),
                //        new KeyboardButton("/removetask")
                        
                //    },
                //    new[]
                //    {
                //        new KeyboardButton("/completetask"),
                //        new KeyboardButton("/find")
                //    },
                //})
                //{
                //    ResizeKeyboard = true,
                //    OneTimeKeyboard = false,
                //    Selective = true,
                //    InputFieldPlaceholder = "Выберите команду или введите текст"
                //};
                //handler.SetCommandKeyboard(commandKeyboard);
                //подписываемся
                handler.OnHandleUpdateStarted += message =>
                    Console.WriteLine($"Началась обработка сообщения '{message}'.");
                handler.OnHandleUpdateCompleted += message =>
                    Console.WriteLine($"Закончилась обработка сообщения '{message}'.");
                botClient.StartReceiving(handler, receiverOptions, cts.Token);
                var me = await botClient.GetMe(cts.Token);
                Console.WriteLine($"{me.FirstName} запущен!");
                Console.WriteLine("Нажмите клавишу A для выхода");
                while (!cts.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.A)
                    {
                        Console.WriteLine("Запущена остановка бота...");
                        cts.Cancel();
                        break;
                    }
                    await Task.Delay(-1);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Бот успешно остановлен.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}.");
            }
            finally
            {
                //отписываемся
                handler.OnHandleUpdateStarted -= message =>
                    Console.WriteLine($"Началась обработка сообщения '{message}'.");
                handler.OnHandleUpdateCompleted -= message =>
                    Console.WriteLine($"Закончилась обработка сообщения '{message}'.");
            }
        }
    }
}