document.addEventListener('DOMContentLoaded', function() {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.userinfo__username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Студент';
    
    const adminLink = document.querySelector('.admin-link, #admin-link');
    if (adminLink && !isTeacher) {
        adminLink.style.display = 'none';
    }
    
    document.querySelectorAll('.tasklist_task-button').forEach(button => {
        button.addEventListener('click', function() {
            const taskType = this.getAttribute('data-task-type');
            if (taskType === 'ab-train') {
                window.location.href = `/TaskSolvePage/TaskSolvePage.html?taskType=train`;
            } else if (taskType === 'ab-test') {
                fetchAssignedTasks('min-max');
            } else if (taskType === 'a-star') {
                window.location.href = `/TaskSolveAPage/TaskSolveAPage.html?taskType=train`;
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
            window.location.href = "/LoginPage/LoginPage.html";
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

async function fetchAssignedTasks(specificTaskType = null) {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const tasks = [];

        if (!specificTaskType || specificTaskType === 'min-max') {
            const minMaxResponse = await fetch(`${apiHost}/AB/Test`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${authtoken}`
                }
            });

            if (minMaxResponse.ok) {
                const taskData = await minMaxResponse.json();
                tasks.push({ ...taskData, taskType: 'min-max' });
            }else    if (minMaxResponse.status === 401) {
                const refreshtoken = Cookies.get('RefreshToken');
                if (isTokenExpired(authtoken)) {
                    refreshToken();
                }
            } 
            else if (minMaxResponse.status !== 404) {
                throw new Error(`Ошибка HTTP (Min-Max): ${minMaxResponse.status}`);
            }
        }

        if (!specificTaskType || specificTaskType === 'a-star') {
            const aStarResponse = await fetch(`${apiHost}/A/FifteenPuzzle/Test`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${authtoken}`
                }
            });

            if (aStarResponse.ok) {
                const taskData = await aStarResponse.json();
                tasks.push({ ...taskData, taskType: 'a-star' });
            }else    if (aStarResponse.status === 401) {
                const refreshtoken = Cookies.get('RefreshToken');
                if (isTokenExpired(authtoken)) {
                    refreshToken();
                } 
            }
            else if (aStarResponse.status !== 404) {
                throw new Error(`Ошибка HTTP (A*): ${aStarResponse.status}`);
            }
        }

        console.log('Полученные задания:', tasks);
        populateTasksTable(tasks);
    } catch (error) {
        console.error('Ошибка в fetchAssignedTasks:', error);
        alert('Произошла ошибка при загрузке заданий');
    }
}

function populateTasksTable(tasks) {
    const tableBody = document.querySelector('.given-tasks-table__table tbody');
    tableBody.innerHTML = '';

    if (!tasks || tasks.length === 0) {
        console.log('Нет заданий для отображения');
        return;
    }

    tasks.forEach(taskData => {
        const taskName = taskData.taskType === 'min-max' ? 'min-max алгоритм' : 'Пятнашки A*';
        const date = new Date(taskData.date || Date.now());
        const formattedDate = `${date.getDate().toString().padStart(2, '0')}.${(date.getMonth() + 1).toString().padStart(2, '0')}.${date.getFullYear()}`;
        const status = taskData.isSolved ? (taskData.userSolution ? 'Проверено' : 'Выполнено') : 'Выдано';
        const taskId = taskData.id || `${taskData.taskType}-task`;

        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${taskName}</td>
            <td>Преподаватель</td>
            <td>${formattedDate}</td>
            <td>${status}</td>
            <td>
                <button class="button-solve" data-task-id="${taskId}" data-task-type="${taskData.taskType}"></button>
            </td>
        `;

        tableBody.appendChild(tr);
    });

    document.querySelectorAll('.button-solve').forEach(button => {
        button.addEventListener('click', function() {
            const taskId = this.dataset.taskId;
            const taskType = this.dataset.taskType;
            const taskData = tasks.find(task => task.id === taskId || `${task.taskType}-task` === taskId);

            console.log('Переход к решению:', { taskId, taskType, taskData });
            sessionStorage.setItem('currentTask', JSON.stringify(taskData));
            window.location.href = taskType === 'min-max' 
                ? `/TaskSolvePage/TaskSolvePage.html?taskType=${taskType}`
                : `/TaskSolveAPage/TaskSolveAPage.html?taskType=${taskType}`;
        });
    });
}