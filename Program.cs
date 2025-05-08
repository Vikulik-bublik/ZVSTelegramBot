using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.Infrastructure.DataAccess;
using ZVSTelegramBot.TelegramBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;


namespace ZVSTelegramBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var userRepository = new InMemoryUserRepository();
            var toDoRepository = new InMemoryToDoRepository();
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
                new() { Command = "start", Description = "Начать работу с ботом" },
                //new() { Command = "addtask", Description = "Добавление задачи" },
                //new() { Command = "removetask", Description = "Удаление задачи" },
                //new() { Command = "completetask", Description = "Отметить задачу выполненной" },
                new() { Command = "showtasks", Description = "Показать активные задачи" },
                new() { Command = "showalltasks", Description = "Показать все задачи" },
                new() { Command = "report", Description = "Статистика по задачам" },
                //new() { Command = "find", Description = "Поиск задачи по префиксу" },
                new() { Command = "help", Description = "Помощь по командам" },
                new() { Command = "info", Description = "Информация о боте" }
            };
            try
            {
                await botClient.SetMyCommands(
                commands: commands,
                cancellationToken: cts.Token);

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
                    await Task.Delay(-1); // Устанавливаем бесконечную задержку
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