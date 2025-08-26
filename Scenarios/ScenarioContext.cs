using System;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ZVSTelegramBot.Scenarios
{

    public class ScenarioContext
    {
        public long UserId { get; }
        public ScenarioType CurrentScenario { get; }
        public string? CurrentStep { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public ScenarioContext(long userId, ScenarioType scenario)
        {
            UserId = userId;
            CurrentScenario = scenario;
        }
    }
}
