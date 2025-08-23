-- 1. GetAllByUserId - Получить все задачи пользователя (делаем все для Ivan)
SELECT 
    "Id", 
    "UserId", 
    "Name", 
    "CreatedAt", 
    "State", 
    "StateChangedAt", 
    "Deadline", 
    "ListId"
FROM "ToDoItem" 
WHERE "UserId" = '123e4567-e89b-12d3-a456-426614174000'
ORDER BY "CreatedAt" DESC;

-- 2. GetActiveByUserId - Получить активные задачи пользователя
SELECT 
    "Id", 
    "UserId", 
    "Name", 
    "CreatedAt", 
    "State", 
    "StateChangedAt", 
    "Deadline", 
    "ListId"
FROM "ToDoItem" 
WHERE "UserId" = '123e4567-e89b-12d3-a456-426614174000'
AND "State" = 0
ORDER BY "CreatedAt" DESC;

-- 3. Find - Ааналог предиката
SELECT 
    "Id", 
    "UserId", 
    "Name", 
    "CreatedAt", 
    "State", 
    "StateChangedAt", 
    "Deadline", 
    "ListId"
FROM "ToDoItem" 
WHERE "UserId" = '123e4567-e89b-12d3-a456-426614174000'
AND "Name" ILIKE '%Тренир%'
ORDER BY "CreatedAt" DESC;

-- 7. ExistsByName - Проверка существования задачи с таким именем у пользователя
SELECT EXISTS 
(
    SELECT 1 
    FROM "ToDoItem" 
    WHERE "UserId" = '123e4567-e89b-12d3-a456-426614174000' 
    AND "Name" = 'Подготовить отчет'
);

-- 9. GetByIdAsync - Получение задачи по ID
SELECT 
    "Id", 
    "UserId", 
    "Name", 
    "CreatedAt", 
    "State", 
    "StateChangedAt", 
    "Deadline", 
    "ListId"
FROM "ToDoItem" 
WHERE "Id" = '823e4567-e89b-12d3-a456-426614174007';


-- 10. Статистика по задачам пользователя
SELECT 
    COUNT(*) as "TotalTasks",
    COUNT(*) FILTER (WHERE "State" = 0) as "ActiveTasks",
    COUNT(*) FILTER (WHERE "State" = 1) as "CompletedTasks",
    COUNT(*) FILTER (WHERE "ListId" IS NULL) as "TasksWithoutList",
    COUNT(*) FILTER (WHERE "Deadline" < CURRENT_TIMESTAMP AND "State" = 0) as "OverdueTasks"
FROM "ToDoItem" 
WHERE "UserId" = '123e4567-e89b-12d3-a456-426614174000';