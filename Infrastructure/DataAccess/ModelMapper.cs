using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess.Models;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    internal static class ModelMapper
    {
        public static ToDoUser MapFromModel(ToDoUserModel model)
        {
            if (model == null) return null;
            return new ToDoUser
            {
                UserId = model.UserId,
                TelegramUserId = model.TelegramUserId,
                TelegramUserName = model.TelegramUserName,
                RegisteredAt = model.RegisteredAt
            };
        }

        public static ToDoUserModel MapToModel(ToDoUser entity)
        {
            if (entity == null) return null;
            return new ToDoUserModel
            {
                UserId = entity.UserId,
                TelegramUserId = entity.TelegramUserId,
                TelegramUserName = entity.TelegramUserName,
                RegisteredAt = entity.RegisteredAt
            };
        }

        public static ToDoItem MapFromModel(ToDoItemModel model)
        {
            if (model == null) return null;
            return new ToDoItem
            {
                Id = model.Id,
                Name = model.Name,
                CreatedAt = model.CreatedAt,
                State = model.State,
                StateChangedAt = model.StateChangedAt,
                Deadline = model.Deadline,
                User = MapFromModel(model.User),
                List = MapFromModel(model.List)
            };
        }

        public static ToDoItemModel MapToModel(ToDoItem entity)
        {
            if (entity == null) return null;
            return new ToDoItemModel
            {
                Id = entity.Id,
                UserId = entity.User?.UserId ?? Guid.Empty,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt,
                State = entity.State,
                StateChangedAt = entity.StateChangedAt,
                Deadline = entity.Deadline,
                ListId = entity.List?.Id,
                User = MapToModel(entity.User),
                List = MapToModel(entity.List)
            };
        }

        public static ToDoList MapFromModel(ToDoListModel model)
        {
            if (model == null) return null;
            return new ToDoList
            {
                Id = model.Id,
                Name = model.Name,
                CreatedAt = model.CreatedAt,
                User = MapFromModel(model.User)
            };
        }

        public static ToDoListModel MapToModel(ToDoList entity)
        {
            if (entity == null) return null;
            return new ToDoListModel
            {
                Id = entity.Id,
                Name = entity.Name,
                UserId = entity.User?.UserId ?? Guid.Empty,
                CreatedAt = entity.CreatedAt,
                User = MapToModel(entity.User)
            };
        }
    }
}
