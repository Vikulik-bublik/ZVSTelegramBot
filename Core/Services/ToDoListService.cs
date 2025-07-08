using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public class ToDoListService : IToDoListService
    {
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ToDoListService(string storagePath)
        {
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            Directory.CreateDirectory(_storagePath);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        public async Task<ToDoList> Add(ToDoUser user, string name, CancellationToken ct)
        {
            await Helper.ValidateString(name, ct);
            if (name.Length > 10)
            throw new ArgumentException("Название списка не может быть длиннее 10 символов");
            var existingLists = await GetUserLists(user.UserId, ct);
            if (existingLists.Any(list => list.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Список с таким названием уже существует у этого пользователя");
            var newList = new ToDoList
            {
                Id = Guid.NewGuid(),
                Name = name,
                User = user,
                CreatedAt = DateTime.UtcNow.AddHours(3)
            };
            var filePath = Path.Combine(_storagePath, $"{newList.Id}.json");
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, newList, _jsonOptions, ct);
            return newList;
        }

        public async Task<ToDoList?> Get(Guid id, CancellationToken ct)
        {
            var filePath = Path.Combine(_storagePath, $"{id}.json");
            if (!File.Exists(filePath))
            return null;
            try
            {
                await using var stream = File.OpenRead(filePath);
                var list = await JsonSerializer.DeserializeAsync<ToDoList>(stream, _jsonOptions, ct);
                return list;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task Delete(Guid id, CancellationToken ct)
        {
            var filePath = Path.Combine(_storagePath, $"{id}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath), ct);
            }
        }

        public async Task<IReadOnlyList<ToDoList>> GetUserLists(Guid userId, CancellationToken ct)
        {
            var lists = new List<ToDoList>();
            foreach (var file in Directory.EnumerateFiles(_storagePath, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(file);
                    var list = await JsonSerializer.DeserializeAsync<ToDoList>(stream, _jsonOptions, ct);
                    if (list?.User?.UserId == userId)
                    {
                        lists.Add(list);
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }
            return lists;
        }
    }
}
