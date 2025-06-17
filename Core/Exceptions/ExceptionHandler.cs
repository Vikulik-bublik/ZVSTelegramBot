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
