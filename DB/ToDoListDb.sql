-- Создание таблицы пользователей
CREATE TABLE "ToDoUser" 
(
    "UserId" UUID PRIMARY KEY,
    "TelegramUserId" BIGINT NOT NULL,
    "TelegramUserName" VARCHAR(255) NOT NULL,
    "RegisteredAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Создание таблицы списков задач
CREATE TABLE "ToDoList" 
(
    "Id" UUID PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "UserId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Создание таблицы задач
CREATE TABLE "ToDoItem" 
(
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "State" INT NOT NULL DEFAULT 0 CHECK ("State" IN (0, 1)), -- 0 = Active, 1 = Completed
    "StateChangedAt" TIMESTAMP NULL,
    "Deadline" TIMESTAMP NULL,
    "ListId" UUID NULL
);

-- Создание таблицы уведомлений
CREATE TABLE "Notifications" 
(
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "Type" VARCHAR(100) NOT NULL,
    "Text" VARCHAR(500) NOT NULL,
    "ScheduledAt" TIMESTAMP NOT NULL,
    "IsNotified" BOOLEAN NOT NULL DEFAULT FALSE,
    "NotifiedAt" TIMESTAMP NULL
);

-- Создание внешних ключей:

-- Внешний ключ: ToDoList.UserId -> ToDoUser.UserId
ALTER TABLE "ToDoList" 
ADD CONSTRAINT "FK_ToDoList_ToDoUser" 
FOREIGN KEY ("UserId") 
REFERENCES "ToDoUser"("UserId") 
ON DELETE CASCADE;

-- Внешний ключ: ToDoItem.UserId -> ToDoUser.UserId
ALTER TABLE "ToDoItem" 
ADD CONSTRAINT "FK_ToDoItem_ToDoUser" 
FOREIGN KEY ("UserId") 
REFERENCES "ToDoUser"("UserId") 
ON DELETE CASCADE;

-- Внешний ключ: ToDoItem.ListId -> ToDoList.Id
ALTER TABLE "ToDoItem" 
ADD CONSTRAINT "FK_ToDoItem_ToDoList" 
FOREIGN KEY ("ListId") 
REFERENCES "ToDoList"("Id") 
ON DELETE SET NULL;

-- Внешний ключ: Notifications.UserId -> ToDoUser.UserId
ALTER TABLE "Notifications" 
ADD CONSTRAINT "FK_Notifications_ToDoUser" 
FOREIGN KEY ("UserId") 
REFERENCES "ToDoUser"("UserId") 
ON DELETE CASCADE;

-- Создание индексов:

-- Уникальный индекс для TelegramUserId
CREATE UNIQUE INDEX "IXU_ToDoUser_TelegramUserId" 
ON "ToDoUser"("TelegramUserId");

-- Индексы для внешних ключей:

-- Для ToDoList.UserId (связь с ToDoUser)
CREATE INDEX "IX_ToDoList_UserId" 
ON "ToDoList"("UserId");

-- Для ToDoItem.UserId (связь с ToDoUser)
CREATE INDEX "IX_ToDoItem_UserId" 
ON "ToDoItem"("UserId");

-- Для ToDoItem.ListId (связь с ToDoList)
CREATE INDEX "IX_ToDoItem_ListId" 
ON "ToDoItem"("ListId");

-- Индекс для поиска задач по статусу
CREATE INDEX "IX_ToDoItem_State" 
ON "ToDoItem"("State");

-- Индекс для UserId (для быстрого поиска уведомлений пользователя)
CREATE INDEX "IX_Notifications_UserId" 
ON "Notifications"("UserId");

-- Индекс для ScheduledAt и IsNotified (для поиска уведомлений, которые нужно отправить)
CREATE INDEX "IX_Notifications_ScheduledAt_IsNotified" 
ON "Notifications"("ScheduledAt", "IsNotified");

-- Индекс для Type (если планируется часто искать уведомления по типу)
CREATE INDEX "IX_Notifications_Type" 
ON "Notifications"("Type");