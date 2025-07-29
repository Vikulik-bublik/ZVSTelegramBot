using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.Exceptions;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class FileToDoListRepository : IToDoListRepository
    {
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileToDoListRepository(string storagePath)
        {
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            Directory.CreateDirectory(_storagePath);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        private string GetListFilePath(Guid listId) => Path.Combine(_storagePath, $"{listId}.json");

        public async Task<ToDoList?> Get(Guid id, CancellationToken ct)
        {
            var filePath = GetListFilePath(id);
            if (!File.Exists(filePath))
            return null;

            try
            {
                await using var fileStream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<ToDoList>(fileStream, _jsonOptions, ct);
            }
            catch (JsonException ex)
            {
                throw new RepositoryException($"Ошибка чтения списка {id}", ex);
            }
        }

        public async Task<IReadOnlyList<ToDoList>> GetByUserId(Guid userId, CancellationToken ct)
        {
            var lists = new List<ToDoList>();

            foreach (var filePath in Directory.EnumerateFiles(_storagePath, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var fileStream = File.OpenRead(filePath);
                    var list = await JsonSerializer.DeserializeAsync<ToDoList>(fileStream, _jsonOptions, ct);

                    if (list?.User != null && list.User.UserId == userId) lists.Add(list);
                }
                catch (JsonException)
                {
                    var errorFolder = Path.Combine(_storagePath, "ErrorFiles"); //не знаю как еще, но пусть лежит в отдельной папке
                    Directory.CreateDirectory(errorFolder);
                    var fileName = Path.GetFileName(filePath);
                    var destPath = Path.Combine(errorFolder, fileName);
                    File.Move(filePath, destPath);
                    continue;
                }
            }
            return lists;
        }

        public async Task Add(ToDoList list, CancellationToken ct)
        {
            if (list == null)
            throw new ArgumentNullException(nameof(list));

            var filePath = GetListFilePath(list.Id);
            if (File.Exists(filePath))
            throw new RepositoryException($"Список с ID {list.Id} уже существует");

            try
            {
                await using var fileStream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(fileStream, list, _jsonOptions, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RepositoryException($"Не удалось сохранить список {list.Id}", ex);
            }
        }
        public async Task Delete(Guid id, CancellationToken ct)
        {
            var filePath = GetListFilePath(id);
            if (!File.Exists(filePath))
            throw new ListNotFoundException(id);

            try
            {
                await Task.Run(() => File.Delete(filePath), ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RepositoryException($"Не удалось удалить список {id}", ex);
            }
        }

        public async Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct)
        {
            var lists = await GetByUserId(userId, ct);
            return lists.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
