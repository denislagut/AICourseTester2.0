document.addEventListener('DOMContentLoaded', () => {
    if (!restrictAccess()) return;

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

        if (!Array.isArray(minMaxTasks) || !Array.isArray(aStarTasks)) {
            throw new Error('Ответ API не является массивом заданий');
        }

        // Combine tasks with task type information
        const tasksWithUserInfo = [
            ...minMaxTasks.map(assignment => ({
                ...assignment.task,
                userId: assignment.user?.id || 'unknown',
                userName: assignment.user ? [assignment.user.secondName, assignment.user.name, assignment.user.patronymic].filter(Boolean).join(' ') : 'Неизвестный пользователь',
                group: assignment.user?.group || '----------',
                taskType: 'min-max'
            })),
            ...aStarTasks.map(assignment => ({
                ...assignment.task,
                userId: assignment.user?.id || 'unknown',
                userName: assignment.user ? [assignment.user.secondName, assignment.user.name, assignment.user.patronymic].filter(Boolean).join(' ') : 'Неизвестный пользователь',
                group: assignment.user?.group || '----------',
                taskType: 'a-star'
            }))
        ];

        console.log('Преобразованные данные:', tasksWithUserInfo); // Диагностика
        populateTasksTable(tasksWithUserInfo);
    } catch (error) {
        console.error('Ошибка в fetchAssignedTasks:', error.message, error.stack);
        alert(`Произошла ошибка при загрузке заданий: ${error.message}`);
    }
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

        const date = new Date(task.date || Date.now());
        const formattedDate = `${date.getDate().toString().padStart(2, '0')}.${(date.getMonth() + 1).toString().padStart(2, '0')}.${date.getFullYear()}`;
        const shortName = formatShortName(task.userName);
        const status = task.isSolved ? 'Выполнено' : 'Выдано';
        const userId = task.userId || 'unknown';
        const taskName = task.taskType === 'min-max' ? 'min-max алгоритм' : 'Пятнашки A*';
        const buttonId = `edit-button-${task.id || 'unknown'}-${index}`; // Уникальный ID для кнопки

        console.log('Создание строки таблицы:', { taskId: task.id, userId, taskType: task.taskType, taskName, buttonId }); // Диагностика

        const viewButton = task.isSolved ? `<button class="button-view" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${task.taskType}" title="Посмотреть решение"></button>` : '';

        tr.innerHTML = `
            <td>${taskName}</td>
            <td>${shortName}</td>
            <td>${task.group}</td>
            <td>${formattedDate}</td>
            <td>${status}</td>
            <td class="actions-cell">
                <button id="${buttonId}" class="button-edit" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${task.taskType}" title="Редактировать"></button>
                <button class="button-delete" data-task-id="${task.id || 'unknown'}" data-user-id="${userId}" data-task-type="${task.taskType}" title="Удалить"></button>
                ${viewButton}
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
        const endpoint = taskType === 'min-max' ? `${apiHost}/AB/Users/${userId}` : `${apiHost}/A/FifteenPuzzle/Users/${userId}`;
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