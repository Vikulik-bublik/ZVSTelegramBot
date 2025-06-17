using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Scenarios
{
    public enum ScenarioResult
    {
        Transition,  // Переход к следующему шагу (сценарий продолжается)
        Completed    // Сценарий завершён
    }
}
