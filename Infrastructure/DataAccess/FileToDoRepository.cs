using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;
using System.Text.Json.Serialization;
using ZVSTelegramBot.Core.Exceptions;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class FileToDoRepository : IToDoRepository
    {
        private readonly string _baseStoragePath;
        private readonly string _indexFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private Dictionary<Guid, Guid> _taskToUserIndex;
        private readonly object _syncRoot = new object();
        public FileToDoRepository(string baseStoragePath)
        {
            _baseStoragePath = baseStoragePath ?? throw new ArgumentNullException(nameof(baseStoragePath));
            _indexFilePath = Path.Combine(_baseStoragePath, "_index.json");
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            Directory.CreateDirectory(_baseStoragePath);
            InitializeIndex();
        }
        private void InitializeIndex()
        {
            if (File.Exists(_indexFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_indexFilePath);
                    _taskToUserIndex = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json) ?? new Dictionary<Guid, Guid>();
                }
                catch
                {
                    _taskToUserIndex = new Dictionary<Guid, Guid>();
                    RebuildIndex();
                }
            }
            else
            {
                _taskToUserIndex = new Dictionary<Guid, Guid>();
                RebuildIndex();
            }
        }
        private void RebuildIndex()
        {
            _taskToUserIndex.Clear();
            foreach (var userDir in Directory.EnumerateDirectories(_baseStoragePath))
            {
                if (Guid.TryParse(Path.GetFileName(userDir), out Guid userId))
                {
                    foreach (var taskFile in Directory.EnumerateFiles(userDir, "*.json"))
                    {
                        if (Guid.TryParse(Path.GetFileNameWithoutExtension(taskFile), out Guid taskId))
                        {
                            _taskToUserIndex[taskId] = userId;
                        }
                    }
                }
            }
            SaveIndex();
        }
        private void SaveIndex()
        {
            var json = JsonSerializer.Serialize(_taskToUserIndex, _jsonOptions);
            File.WriteAllText(_indexFilePath, json);
        }
        private string GetUserDirectoryPath(Guid userId) => Path.Combine(_baseStoragePath, userId.ToString());
        private string GetTaskFilePath(Guid userId, Guid taskId) => Path.Combine(GetUserDirectoryPath(userId), $"{taskId}.json");
        private void EnsureUserDirectoryExists(Guid userId)
        {
            var userDir = GetUserDirectoryPath(userId);
            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
            }
        }
        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct)
        {
            var userDir = GetUserDirectoryPath(userId);
            if (!Directory.Exists(userDir)) return new List<ToDoItem>();
            var tasks = new List<ToDoItem>();
            foreach (var filePath in Directory.EnumerateFiles(userDir, "*.json"))
            {
                await using var fileStream = File.OpenRead(filePath);
                var item = await JsonSerializer.DeserializeAsync<ToDoItem>(fileStream, _jsonOptions, ct);
                if (item != null) tasks.Add(item);
            }
            return tasks;
        }
        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct)
        {
            var userDir = GetUserDirectoryPath(userId);
            if (!Directory.Exists(userDir)) return new List<ToDoItem>();
            var tasks = new List<ToDoItem>();
            foreach (var filePath in Directory.EnumerateFiles(userDir, "*.json"))
            {
                await using var fileStream = File.OpenRead(filePath);
                var item = await JsonSerializer.DeserializeAsync<ToDoItem>(fileStream, _jsonOptions, ct);
                if (item != null && item.State == ToDoItemState.Active)
                    tasks.Add(item);
            }
            return tasks;
        }
        public async Task<IReadOnlyList<ToDoItem>> Find(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            var allTasks = await GetAllByUserId(userId, ct);
            return allTasks.Where(predicate).ToList();
        }
        public async Task Add(ToDoItem item, CancellationToken ct)
        {
            if (item.User == null)
                throw new ArgumentException("Пользователь не найден");
            var userId = item.User.UserId;
            var taskId = item.Id;
            lock (_syncRoot)
            {
                EnsureUserDirectoryExists(userId);
                var filePath = GetTaskFilePath(userId, taskId);
                _taskToUserIndex[taskId] = userId;
                SaveIndex();
            }

            await using (var fileStream = File.Create(GetTaskFilePath(userId, taskId)))
            {
                await JsonSerializer.SerializeAsync(fileStream, item, _jsonOptions, ct);
            }
        }
        public async Task Update(ToDoItem item, CancellationToken ct)
        {
            if(item.User == null)
                throw new ArgumentException("Пользователь не найден");

            var filePath = GetTaskFilePath(item.User.UserId, item.Id);
            if (!File.Exists(filePath))
                throw new TaskNotFoundException(item.Id);

            await using (var fileStream = File.Create(filePath))
            {
                await JsonSerializer.SerializeAsync(fileStream, item, _jsonOptions, ct);
            }
        }
        public async Task Delete(Guid id, CancellationToken ct)
        {
            var task = await GetByIdAsync(id, ct);
            if (task == null)
                throw new TaskNotFoundException(id);

            string filePath;
            lock (_syncRoot)
            {
                if (!_taskToUserIndex.TryGetValue(id, out var userId))
                    throw new TaskNotFoundException(id);
                filePath = Path.Combine(GetUserDirectoryPath(userId), $"{id}.json");
                _taskToUserIndex.Remove(id);
                SaveIndex();
            }
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath), ct);
                }
        }
        public async Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct)
        {
            var allTasks = await GetAllByUserId(userId, ct);
            return allTasks.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public async Task<int> CountActive(Guid userId, CancellationToken ct)
        {
            var allTasks = await GetAllByUserId(userId, ct);
            return allTasks.Count(t => t.State == ToDoItemState.Active);
        }
        public async Task<ToDoItem?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            string filePath;
            lock (_syncRoot)
            {
                filePath = GetTaskFilePath(id);
            }

            if (!File.Exists(filePath))
                return null;

            await using var fileStream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<ToDoItem>(fileStream, _jsonOptions, ct);
        }
        private string GetTaskFilePath(Guid taskId)
        {
            if (!_taskToUserIndex.TryGetValue(taskId, out var userId))
                throw new TaskNotFoundException(taskId);
            return Path.Combine(GetUserDirectoryPath(userId), $"{taskId}.json");
        }
    }
}
