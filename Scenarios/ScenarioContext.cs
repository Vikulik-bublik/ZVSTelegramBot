using System;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ZVSTelegramBot.Scenarios
{

    public class ScenarioContext
    {
        public long UserId { get; set; }
        public ScenarioType CurrentScenario { get; set; }
        public string? CurrentStep { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();

        public ScenarioContext(long userId, ScenarioType scenario)
        {
            UserId = userId;
            CurrentScenario = scenario;
        }
    }
}
