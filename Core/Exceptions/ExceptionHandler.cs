using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Core.Exceptions
{
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
        public RepositoryException(string message, Exception inner) : base(message, inner) { }
    }
    public class TaskCountLimitException : Exception
    {
        public TaskCountLimitException(int MaxTaskCount) : base($"Максимальное количество задач - {MaxTaskCount}, вы можете воспользоваться командой /removetask <номер задачи>") { }
    }
    public class TaskLengthLimitException : Exception
    {
        public TaskLengthLimitException(int MaxLengthCount) : base($"Максимальная длина задачи - {MaxLengthCount}") { }
    }
    public class DuplicateTaskException : Exception
    {
        public DuplicateTaskException(string task) : base($"Задача - {task} уже существует") { }
    }
    public class TaskNotFoundException : RepositoryException
    {
        public TaskNotFoundException(Guid taskId)
            : base($"Задача с ID {taskId} не найдена")
        {
        }
    }
    public class UserNotFoundException : RepositoryException
    {
        public UserNotFoundException(Guid userId)
            : base($"Пользователь с ID {userId} не найден")
        {
        }
    }
}
