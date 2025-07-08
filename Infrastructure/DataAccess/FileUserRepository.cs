using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;
using System.Text.Json.Serialization;
using ZVSTelegramBot.Core.Exceptions;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class FileUserRepository : IUserRepository
    {
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions;
        public FileUserRepository(string storagePath)
        {
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            Directory.CreateDirectory(_storagePath);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }
        private string GetUserFilePath(Guid userId) => Path.Combine(_storagePath, $"{userId}.json");
        public async Task<ToDoUser?> GetUser(Guid userId, CancellationToken ct)
        {
            var filePath = GetUserFilePath(userId);
            if (!File.Exists(filePath))
                return null;
            try
            {
                await using var fileStream = File.OpenRead(filePath);
                return await JsonSerializer.DeserializeAsync<ToDoUser>(fileStream, _jsonOptions, ct);
            }
            catch (JsonException ex)
            {
                throw new RepositoryException($"Ошибка чтения пользователя {userId}", ex);
            }
        }
        public async Task<ToDoUser?> GetUserByTelegramUserId(long telegramUserId, CancellationToken ct)
        {
            foreach (var filePath in Directory.EnumerateFiles(_storagePath, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var fileStream = File.OpenRead(filePath);
                    var user = await JsonSerializer.DeserializeAsync<ToDoUser>(fileStream, _jsonOptions, ct);

                    if (user?.TelegramUserId == telegramUserId)
                        return user;
                }
                catch (JsonException)
                {
                    continue;
                }
            }
            return null;
        }
        public async Task Add(ToDoUser user, CancellationToken ct)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            var existingUser = await GetUserByTelegramUserId(user.TelegramUserId, ct);
            if (existingUser != null)
                throw new RepositoryException($"Пользователь с Telegram ID {user.TelegramUserId} уже существует");
            var filePath = GetUserFilePath(user.UserId);
            try
            {
                await using var fileStream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(fileStream, user, _jsonOptions, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RepositoryException($"Не удалось сохранить пользователя {user.UserId}", ex);
            }
        }
        public async Task UpdateUser(ToDoUser user, CancellationToken ct)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            var filePath = GetUserFilePath(user.UserId);
            if (!File.Exists(filePath))
                throw new UserNotFoundException(user.UserId);
            try
            {
                await using var fileStream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(fileStream, user, _jsonOptions, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RepositoryException($"Не удалось обновить данные пользователя {user.UserId}", ex);
            }
        }
    }
}
