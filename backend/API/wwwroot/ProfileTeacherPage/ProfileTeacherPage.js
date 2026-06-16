document.addEventListener('DOMContentLoaded', () => {
    if (!restrictTeacherAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.userinfo__username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Преподаватель';
    fetchAssignedTasks();

    // Добавляем обработчики для кнопок в tasklist__content
    const taskButtons = document.querySelectorAll('.tasklist_task-button');
    taskButtons.forEach(button => {
        button.addEventListener('click', handleTaskButtonClick);
    });
});

document.getElementById('profile-tooltip__button-logout').addEventListener('click', e => {
    e.preventDefault();
    Logout();
});

async function Logout() {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const response = await fetch(`${apiHost}/Users/Logout`, {
            method: 'POST',
            headers: {
                Authorization: `Bearer ${authtoken}`
            }
        });
        if (response.status === 200) {
            sessionStorage.removeItem('userFullName');
            window.location.href = "/LoginPage/LoginPage.html";
        } else {
            console.error('Ошибка выхода:', response.status, response.statusText);
            alert('Не удалось выйти из системы');
        }
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
    } catch (error) {
        console.error('Ошибка при выходе:', error);
        alert('Произошла ошибка при выходе');
    }
}

async function fetchAssignedTasks() {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');

        if (!authtoken) {
            throw new Error('Токен авторизации отсутствует');
        }

        // Fetch Min-Max tasks
        const minMaxResponse = await fetch(`${apiHost}/AB/Users/`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        // Fetch A* Fifteen Puzzle tasks
        const aStarResponse = await fetch(`${apiHost}/A/FifteenPuzzle/Users/`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (!minMaxResponse.ok) {
            throw new Error(`Ошибка HTTP (Min-Max): ${minMaxResponse.status} ${minMaxResponse.statusText}`);
        }
        if (!aStarResponse.ok) {
            throw new Error(`Ошибка HTTP (A*): ${aStarResponse.status} ${aStarResponse.statusText}`);
        }
        if (minMaxResponse.status === 401 || aStarResponse.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        const minMaxTasks = await minMaxResponse.json();
        const aStarTasks = await aStarResponse.json();
        const historyTasks = [];

        if (!Array.isArray(minMaxTasks) || !Array.isArray(aStarTasks)) {
            throw new Error('Ответ API не является массивом заданий');
        }

        const currentTasks = [
            ...minMaxTasks.map(assignment => ({
                ...assignment.task,
                userId: assignment.user?.id || 'unknown',
                userName: assignment.user ? [assignment.user.secondName, assignment.user.name, assignment.user.patronymic].filter(Boolean).join(' ') : 'Неизвестный пользователь',
                group: assignment.user?.group || '----------',
                taskType: 'min-max',
                isHistory: false
            })),
            ...aStarTasks.map(assignment => ({
                ...assignment.task,
                userId: assignment.user?.id || 'unknown',
                userName: assignment.user ? [assignment.user.secondName, assignment.user.name, assignment.user.patronymic].filter(Boolean).join(' ') : 'Неизвестный пользователь',
                group: assignment.user?.group || '----------',
                taskType: 'a-star',
                isHistory: false
            }))
        ];
        const tasksWithUserInfo = mergeCurrentAndHistoryTasks(currentTasks, historyTasks);

        console.log('Преобразованные данные:', tasksWithUserInfo); // Диагностика
        populateTasksTable(tasksWithUserInfo);
    } catch (error) {
        console.error('Ошибка в fetchAssignedTasks:', error.message, error.stack);
        alert(`Произошла ошибка при загрузке заданий: ${error.message}`);
    }
}

async function fetchCompletedTaskHistory(authtoken) {
    const response = await fetch(`${apiHost}/Users/TaskHistory/All`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.ok) {
        const history = await response.json();
        return Array.isArray(history) ? history : [];
    }

    if (response.status === 401) {
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
        return [];
    }

    if (response.status === 404) {
        console.warn('История заданий пока недоступна. Перезапустите backend, чтобы появился endpoint /api/Users/TaskHistory/All.');
        return [];
    }

    throw new Error(`Ошибка HTTP (история заданий): ${response.status} ${response.statusText}`);
}

function mergeCurrentAndHistoryTasks(currentTasks, historyTasks) {
    const normalizedHistory = historyTasks.map(task => ({
        id: task.id || task.Id,
        taskId: task.taskId || task.TaskId,
        taskType: normalizeTaskType(task.taskType || task.TaskType),
        taskName: task.taskName || task.TaskName,
        userId: task.userId || task.UserId || 'unknown',
        userName: task.userName || task.UserName || 'Без имени',
        group: task.groupName || task.GroupName || '----------',
        date: task.date || task.Date,
        status: task.status || task.Status || 'Проверено',
        isSolved: true,
        isHistory: true,
        canOpen: false
    }));

    const skippedHistoryIds = new Set();

    currentTasks.forEach(currentTask => {
        if (!isTaskSolved(currentTask)) {
            return;
        }

        const currentTaskId = currentTask.id || currentTask.Id;
        const currentTaskType = normalizeTaskType(currentTask.taskType || currentTask.TaskType);
        const matchingHistory = normalizedHistory.find(historyTask =>
            !skippedHistoryIds.has(historyTask.id) &&
            historyTask.userId === currentTask.userId &&
            historyTask.taskType === currentTaskType &&
            (!currentTaskId || !historyTask.taskId || String(historyTask.taskId) === String(currentTaskId)));

        if (matchingHistory) {
            skippedHistoryIds.add(matchingHistory.id);
        }
    });

    const visibleHistory = normalizedHistory
        .filter(task => !skippedHistoryIds.has(task.id));

    return [...currentTasks, ...visibleHistory]
        .sort((left, right) => {
            const leftHasActions = hasTaskActions(left);
            const rightHasActions = hasTaskActions(right);

            if (leftHasActions !== rightHasActions) {
                return leftHasActions ? -1 : 1;
            }

            return new Date(getTaskDate(right) || 0) - new Date(getTaskDate(left) || 0);
        });
}

function populateTasksTable(tasks) {
    const tableBody = document.querySelector('.given-tasks-table__table tbody');
    if (!tableBody) {
        console.error('Элемент tbody не найден');
        return;
    }
    tableBody.innerHTML = '';

    tasks.forEach((task, index) => {
        const tr = document.createElement('tr');
        tr.style.whiteSpace = 'nowrap';
        if (task.isHistory) {
            tr.classList.add('task-history-row');
        }

        const date = new Date(getTaskDate(task) || Date.now());
        const formattedDate = `${date.getDate().toString().padStart(2, '0')}.${(date.getMonth() + 1).toString().padStart(2, '0')}.${date.getFullYear()}`;
        const shortName = formatShortName(task.userName);
        const status = task.status || (isTaskSolved(task) ? 'Выполнено' : 'Выдано');
        const userId = task.userId || 'unknown';
        const taskType = normalizeTaskType(task.taskType || task.TaskType);
        const taskName = task.taskName || (taskType === 'min-max' ? 'min-max алгоритм' : 'Пятнашки A*');
        const buttonId = `edit-button-${task.id || 'unknown'}-${index}`; // Уникальный ID для кнопки

        console.log('Создание строки таблицы:', { taskId: task.id, userId, taskType, taskName, buttonId, isHistory: task.isHistory }); // Диагностика

        const actionContent = task.isHistory
            ? '<span class="task-history-action">-</span>'
            : `
                <button id="${buttonId}" class="button-edit" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${taskType}" title="Редактировать"></button>
                <button class="button-delete" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${taskType}" title="Удалить"></button>
                ${isTaskSolved(task) ? `<button class="button-view" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${taskType}" title="Посмотреть решение"></button>` : ''}
            `;

        tr.innerHTML = `
            <td>${taskName}</td>
            <td>${shortName}</td>
            <td>${task.group}</td>
            <td>${formattedDate}</td>
            <td>${status}</td>
            <td class="actions-cell">
                ${actionContent}
            </td>
        `;

        tableBody.appendChild(tr);
    });

    document.querySelectorAll('.button-delete').forEach(button => {
        button.addEventListener('click', handleDeleteTask);
    });
    document.querySelectorAll('.button-edit').forEach(button => {
        button.addEventListener('click', handleEditTask);
    });
    document.querySelectorAll('.button-view').forEach(button => {
        button.addEventListener('click', handleViewSolution);
    });
}

async function handleDeleteTask(e) {
    const button = e.currentTarget;
    const taskId = button.dataset.taskId;
    const userId = button.dataset.userId;
    const taskType = button.dataset.taskType;

    if (!confirm('Вы уверены, что хотите удалить это задание?')) {
        return;
    }

    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const endpoint = taskType === 'min-max'
            ? `${apiHost}/AB/Users/${userId}/Tasks/${taskId}`
            : `${apiHost}/A/FifteenPuzzle/Users/${userId}/Tasks/${taskId}`;
        const response = await fetch(endpoint, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (response.ok) {
            alert('Задание успешно удалено');
            button.closest('tr').remove();
        } else {
            throw new Error(`Ошибка HTTP: ${response.status} ${response.statusText}`);
        }
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
    } catch (error) {
        console.error('Ошибка при удалении:', error);
        alert('Не удалось удалить задание');
    }
}

function handleEditTask(e) {
    const button = e.currentTarget;
    const taskId = button.dataset.taskId;
    const userId = button.dataset.userId;
    const taskType = button.dataset.taskType || 'min-max';
    const userName = button.closest('tr').querySelector('td:nth-child(2)').textContent;
    const userGroup = button.closest('tr').querySelector('td:nth-child(3)').textContent;

    console.log('handleEditTask:', { buttonId: button.id, taskId, userId, taskType, userName, userGroup }); // Диагностика

    if (!taskType || !['min-max', 'a-star'].includes(taskType)) {
        console.error('Некорректный taskType:', taskType);
        alert('Ошибка: неизвестный тип задачи');
        return;
    }

    window.location.href = `/TaskEditPage/TaskEditPage.html?userId=${userId}&userName=${encodeURIComponent(userName)}&userGroup=${encodeURIComponent(userGroup)}&taskId=${taskId}&taskType=${taskType}`;
}

function handleViewSolution(e) {
    const button = e.currentTarget;
    const taskId = button.dataset.taskId;
    const userId = button.dataset.userId;
    const taskType = button.dataset.taskType;

    console.log(`Просмотр решения: Task ID: ${taskId}, User ID: ${userId}, Task Type: ${taskType}`);
    const baseUrl = taskType === 'min-max' 
        ? '/TaskSolvePage/TaskSolvePage.html' 
        : '/TaskSolveAPage/TaskSolveAPage.html';
    window.location.href = `${baseUrl}?view=true&taskId=${taskId}&userId=${userId}&taskType=${taskType}`;
}

function handleTaskButtonClick(e) {
    const button = e.currentTarget;
    const taskType = button.textContent.includes('Пятнашки A*') ? 'a-star' : 'min-max';

    console.log('handleTaskButtonClick:', { taskType }); // Диагностика

    window.location.href = `/TaskEditPage/TaskEditPage.html?taskType=${taskType}`;
}

function formatShortName(fullName) {
    if (!fullName) return 'Без имени';

    const parts = fullName.split(' ').filter(Boolean);

    if (parts.length >= 3) {
        return `${parts[0]} ${parts[1][0]}.${parts[2][0]}.`;
    } else if (parts.length === 2) {
        return `${parts[0]} ${parts[1][0]}.`;
    } else if (parts.length === 1) {
        return parts[0];
    }

    return fullName;
}

function normalizeTaskType(taskType) {
    return taskType === 'FifteenPuzzle' || taskType === 'a-star'
        ? 'a-star'
        : 'min-max';
}

function isTaskSolved(task) {
    return task.isSolved === true || task.IsSolved === true;
}

function getTaskDate(task) {
    return task.date || task.Date || task.completedAt || task.CompletedAt;
}

function hasTaskActions(task) {
    return !task.isHistory;
}
