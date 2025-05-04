using ConsoleBot.Core.DataAccess;
using ConsoleBot.Core.Services;
using ConsoleBot.Infrastructure.DataAccess;
using ConsoleBot.TelegramBot;
using Telegram.Bot;
using Telegram.Bot.Types;


namespace ConsoleBot
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var userRepository = new InMemoryUserRepository();
            var toDoRepository = new InMemoryToDoRepository();
            var reportService = new ToDoReportService(toDoRepository);
            var userService = new UserService(userRepository);
            var toDoService = new ToDoService(toDoRepository);
            var handler = new UpdateHandler(userService, toDoService, reportService);
            var botClient = new TelegramBotClient("");
            using var cts = new CancellationTokenSource();
            //подписываемся
            handler.OnHandleUpdateStarted += message =>
                Console.WriteLine($"Началась обработка сообщения '{message}'");
            handler.OnHandleUpdateCompleted += message =>
                Console.WriteLine($"Закончилась обработка сообщения '{message}'");

            try
            {
                botClient.StartReceiving(handler, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
            }
            finally
            {
                //отписываемся
                handler.OnHandleUpdateStarted -= message =>
                    Console.WriteLine($"Началась обработка сообщения '{message}'");
                handler.OnHandleUpdateCompleted -= message =>
                    Console.WriteLine($"Закончилась обработка сообщения '{message}'");
            }
        }
    }
}