document.addEventListener('DOMContentLoaded', function() {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.userinfo__username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Студент';

    document.querySelectorAll('.tasklist_task-button').forEach(button => {
        button.addEventListener('click', function() {
            const taskType = this.getAttribute('data-task-type');
            if (taskType === 'ab-train') {
                window.location.href = '/TaskSolvePage/TaskSolvePage.html?taskType=train';
            } else if (taskType === 'ab-test') {
                fetchAssignedTasks('min-max');
            } else if (taskType === 'a-star') {
                window.location.href = '/TaskSolveAPage/TaskSolveAPage.html?taskType=train';
            }
        });
    });

    fetchAssignedTasks();
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
            window.location.href = '/LoginPage/LoginPage.html';
        }

        if (response.status === 401 && isTokenExpired(authtoken)) {
            refreshToken();
        }
    } catch (error) {
        console.error('Ошибка при выходе:', error);
        alert('Произошла ошибка при выходе');
    }
}

async function fetchAssignedTasks(specificTaskType = null) {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const currentTasks = [];

        if (!specificTaskType || specificTaskType === 'min-max') {
            const task = await fetchCurrentTask(`${apiHost}/AB/Test`, 'min-max', authtoken);
            if (task) {
                currentTasks.push(task);
            }
        }

        if (!specificTaskType || specificTaskType === 'a-star') {
            const task = await fetchCurrentTask(`${apiHost}/A/FifteenPuzzle/Test`, 'a-star', authtoken);
            if (task) {
                currentTasks.push(task);
            }
        }

        const historyTasks = await fetchCompletedTaskHistory(authtoken, specificTaskType);
        const tasks = mergeCurrentAndHistoryTasks(currentTasks, historyTasks);

        console.log('Полученные задания:', tasks);
        populateTasksTable(tasks);
    } catch (error) {
        console.error('Ошибка в fetchAssignedTasks:', error);
        alert('Произошла ошибка при загрузке заданий');
    }
}

async function fetchCurrentTask(url, taskType, authtoken) {
    const response = await fetch(url, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.ok) {
        const taskData = await response.json();
        return {
            ...taskData,
            taskType,
            canOpen: true,
            isHistory: false
        };
    }

    if (response.status === 401) {
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
        return null;
    }

    if (response.status === 404) {
        return null;
    }

    throw new Error(`Ошибка HTTP (${taskType}): ${response.status}`);
}

async function fetchCompletedTaskHistory(authtoken, specificTaskType = null) {
    const response = await fetch(`${apiHost}/Users/TaskHistory`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.ok) {
        const history = await response.json();
        return (history || [])
            .filter(task => !specificTaskType || normalizeTaskType(task.taskType || task.TaskType) === specificTaskType);
    }

    if (response.status === 401) {
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
        return [];
    }

    throw new Error(`Ошибка HTTP (история заданий): ${response.status}`);
}

function mergeCurrentAndHistoryTasks(currentTasks, historyTasks) {
    const history = historyTasks.map(task => ({
        ...task,
        taskType: normalizeTaskType(task.taskType || task.TaskType),
        isHistory: true,
        canOpen: false
    }));

    const openableHistoryRunIds = new Set();

    currentTasks.forEach(currentTask => {
        const currentTaskType = normalizeTaskType(currentTask.taskType || currentTask.TaskType);
        const currentTaskId = currentTask.id || currentTask.Id;
        const matchingHistory = history.find(historyTask => {
            const historyTaskType = normalizeTaskType(historyTask.taskType || historyTask.TaskType);
            const historyTaskId = historyTask.taskId || historyTask.TaskId;

            return historyTaskType === currentTaskType &&
                (!currentTaskId || !historyTaskId || String(historyTaskId) === String(currentTaskId));
        });

        if (matchingHistory) {
            openableHistoryRunIds.add(matchingHistory.id || matchingHistory.Id);
        }
    });

    const historyWithActions = history.map(task => ({
        ...task,
        canOpen: openableHistoryRunIds.has(task.id || task.Id)
    }));

    const visibleCurrentTasks = currentTasks
        .filter(task => {
            if (!isTaskSolved(task)) {
                return true;
            }

            return !historyWithActions.some(historyTask =>
                historyTask.taskType === task.taskType &&
                (!task.id || String(historyTask.taskId || historyTask.TaskId) === String(task.id)));
        })
        .map(task => ({
            ...task,
            isHistory: false,
            canOpen: true
        }));

    return [...visibleCurrentTasks, ...historyWithActions]
        .sort((left, right) => new Date(getTaskDate(right) || 0) - new Date(getTaskDate(left) || 0));
}

function populateTasksTable(tasks) {
    const tableBody = document.querySelector('.given-tasks-table__table tbody');
    tableBody.innerHTML = '';

    if (!tasks || tasks.length === 0) {
        tableBody.innerHTML = '<tr><td colspan="5">Задания пока не найдены</td></tr>';
        return;
    }

    tasks.forEach(taskData => {
        const taskType = normalizeTaskType(taskData.taskType || taskData.TaskType);
        const taskName = taskData.taskName || taskData.TaskName || getTaskName(taskType);
        const formattedDate = formatDate(getTaskDate(taskData));
        const status = taskData.status || taskData.Status || getTaskStatus(taskData);
        const teacherName = taskData.teacherName || taskData.TeacherName || 'Преподаватель';
        const taskId = taskData.id || taskData.Id || `${taskType}-task`;
        const canOpen = isTaskOpenable(taskData);
        const actionContent = canOpen
            ? `<button class="button-solve" data-task-id="${taskId}" data-task-type="${taskType}" title="Посмотреть решение"></button>`
            : '<span class="task-history-action">-</span>';

        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${taskName}</td>
            <td>${teacherName}</td>
            <td>${formattedDate}</td>
            <td>${status}</td>
            <td>${actionContent}</td>
        `;

        tableBody.appendChild(tr);
    });

    document.querySelectorAll('.button-solve').forEach(button => {
        button.addEventListener('click', function() {
            const taskId = this.dataset.taskId;
            const taskType = this.dataset.taskType;
            const taskData = tasks.find(task =>
                String(task.id || task.Id || `${normalizeTaskType(task.taskType || task.TaskType)}-task`) === taskId);

            console.log('Переход к решению:', { taskId, taskType, taskData });
            sessionStorage.setItem('currentTask', JSON.stringify(taskData));
            window.location.href = taskType === 'min-max'
                ? `/TaskSolvePage/TaskSolvePage.html?taskType=${taskType}`
                : `/TaskSolveAPage/TaskSolveAPage.html?taskType=${taskType}`;
        });
    });
}

function normalizeTaskType(taskType) {
    return taskType === 'FifteenPuzzle' || taskType === 'a-star'
        ? 'a-star'
        : 'min-max';
}

function getTaskName(taskType) {
    return taskType === 'min-max'
        ? 'min-max алгоритм'
        : 'Пятнашки A*';
}

function getTaskDate(taskData) {
    return taskData.date || taskData.Date || taskData.completedAt || taskData.CompletedAt;
}

function isTaskSolved(taskData) {
    return taskData.isSolved === true || taskData.IsSolved === true;
}

function isTaskOpenable(taskData) {
    return taskData.canOpen !== false && taskData.CanOpen !== false;
}

function formatDate(dateValue) {
    const date = new Date(dateValue || Date.now());
    return `${date.getDate().toString().padStart(2, '0')}.${(date.getMonth() + 1).toString().padStart(2, '0')}.${date.getFullYear()}`;
}

function getTaskStatus(taskData) {
    if (!isTaskSolved(taskData)) {
        return 'Выдано';
    }

    return taskData.userSolution || taskData.UserSolution || taskData.isHistory
        ? 'Проверено'
        : 'Выполнено';
}
