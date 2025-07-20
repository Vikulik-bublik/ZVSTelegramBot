using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.Infrastructure.DataAccess;
using ZVSTelegramBot.Scenarios;
using ZVSTelegramBot.TelegramBot;


namespace ZVSTelegramBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            //хранилища
            var usersStoragePath = Path.Combine(Environment.CurrentDirectory, "Users");
            var userRepository = new FileUserRepository(usersStoragePath);
            var storagePath = Path.Combine(Environment.CurrentDirectory, "ToDoItems");
            var toDoRepository = new FileToDoRepository(storagePath);
            var listsStoragePath = Path.Combine(Environment.CurrentDirectory, "ToDoLists");
            var toDoListRepository = new FileToDoListRepository(listsStoragePath);
            //сервисы
            var reportService = new ToDoReportService(toDoRepository);
            var userService = new UserService(userRepository);
            var toDoService = new ToDoService(toDoRepository);
            var repository = new FileToDoListRepository("path/to/storage");
            var toDoListService = new ToDoListService(repository);
            //сценарии
            var contextRepository = new InMemoryScenarioContextRepository();
            var scenarios = new List<IScenario>
            {
                new AddTaskScenario(userService, toDoService, toDoListService),
                new AddListScenario(userService, toDoListService),
                new DeleteListScenario(userService, toDoListService, toDoService)
            };
            //настройка обновлений
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true
            };
            var handler = new UpdateHandler(userService, toDoService, reportService, scenarios, toDoListService, contextRepository);
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
                new() { Command = "show", Description = "Показать списки с задачами" },
                new() { Command = "report", Description = "Статистика по задачам" },
                new() { Command = "find", Description = "Поиск задачи по префиксу" },
                new() { Command = "help", Description = "Помощь по командам" },
                new() { Command = "info", Description = "Информация о боте" },
                new() { Command = "cancel", Description = "Отмена текущего действия" }
            };
            try
            {
                await botClient.SetMyCommands(commands, cancellationToken: cts.Token);
                
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