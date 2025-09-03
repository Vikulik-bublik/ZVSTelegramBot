using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using ZVSTelegramBot.Scenarios;

namespace ZVSTelegramBot.BackgroundTasks
{
    public class ResetScenarioBackgroundTask : BackgroundTask
    {
        private readonly TimeSpan _resetScenarioTimeout;
        private readonly IScenarioContextRepository _scenarioRepository;
        private readonly ITelegramBotClient _bot;

        public ResetScenarioBackgroundTask(
            TimeSpan resetScenarioTimeout,
            IScenarioContextRepository scenarioRepository,
            ITelegramBotClient bot)
            : base(TimeSpan.FromHours(1), nameof(ResetScenarioBackgroundTask))
        {
            _resetScenarioTimeout = resetScenarioTimeout;
            _scenarioRepository = scenarioRepository;
            _bot = bot;
        }

        protected override async Task Execute(CancellationToken ct)
        {
            var contexts = await _scenarioRepository.GetContexts(ct);

            var oldContexts = contexts
                .Where(context => DateTime.UtcNow - context.CreatedAt > _resetScenarioTimeout)
                .ToList();

            foreach (var context in oldContexts)
            {
                try
                {
                    await _scenarioRepository.ResetContext(context.UserId, ct);
                    await _bot.SendMessage(
                        chatId: context.UserId,
                        text: $"Сценарий отменен, так как не поступил ответ в течение {_resetScenarioTimeout}",
                        replyMarkup: Helper.GetAuthorizedKeyboard(),
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: сброс сценария пользователя {context.UserId}: {ex.Message}");
                }
            }
        }
    }
}
