using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ZVSTelegramBot.Core;
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
            //получение строки подключения к БД из переменных окружения
            var connectionString = Environment.GetEnvironmentVariable("TODO_DB_CONNECTION_STRING", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Строка подключения к БД не найдена в переменных окружения");
                return;
            }

            //получение токена бота из переменных окружения
            string token = Environment.GetEnvironmentVariable("TELEGRAM_CsharpBOT_TOKEN", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Токен бота не найденв переменных окружения");
                return;
            }
            //создаем фабрику контекста данных
            var contextFactory = new DataContextFactory(connectionString);

            //проверка подключения к БД
            try
            {
                using var testContext = contextFactory.CreateDataContext();
                var testUser = await testContext.ToDoUsers.FirstOrDefaultAsync();
                Console.WriteLine("Подключение к БД успешно установлено");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения к БД: {ex.Message}");
                Console.WriteLine("Проверьте строку подключения и доступность PostgreSQL");
                return;
            }

            //репозитории
            IUserRepository userRepository = new SqlUserRepository(contextFactory);
            IToDoRepository toDoRepository = new SqlToDoRepository(contextFactory);
            IToDoListRepository toDoListRepository = new SqlToDoListRepository(contextFactory);

            //сервисы
            var reportService = new ToDoReportService(toDoRepository);
            var userService = new UserService(userRepository);
            var toDoService = new ToDoService(toDoRepository);
            var toDoListService = new ToDoListService(toDoListRepository);

            //сценарии
            var scenarios = new List<IScenario>
            {
                new AddTaskScenario(userService, toDoService, toDoListService),
                new DeleteTaskScenario(userService, toDoService),
                new AddListScenario(userService, toDoListService),
                new DeleteListScenario(userService, toDoListService, toDoService)
            };

            //зависимости
            var contextRepository = new InMemoryScenarioContextRepository();

            //настройка обновлений
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true
            };
            var handler = new UpdateHandler(userService, toDoService, reportService, scenarios, toDoListService, contextRepository);
            var botClient = new TelegramBotClient(token);
            using var cts = new CancellationTokenSource();
            
            //меню команд
            var commands = new List<BotCommand>
            {
                new() { Command = "start", Description = "Начать работу с ботом/заново установить лимиты для задач" },
                new() { Command = "addtask", Description = "Добавление задачи" },
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
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
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