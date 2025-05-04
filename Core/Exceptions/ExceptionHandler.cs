using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBot.Core.Exceptions
{
    public class TaskCountLimitException : Exception
    {
        public TaskCountLimitException(int MaxTaskCount) : base($"Максимальное количество задач - {MaxTaskCount}, вы можете воспользоваться командой /removetask <номер задачи>.") { }
    }
    public class TaskLengthLimitException : Exception
    {
        public TaskLengthLimitException(int MaxLengthCount) : base($"Максимальная длина задачи - {MaxLengthCount}.") { }
    }
    public class DuplicateTaskException : Exception
    {
        public DuplicateTaskException(string task) : base($"Задача - {task} уже существует.") { }
    }
}
