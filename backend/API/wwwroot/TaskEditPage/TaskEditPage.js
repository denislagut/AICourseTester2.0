function joinFullName(data) {
    data.forEach(row => {
        row.name = [row.secondName, row.name, row.patronymic].join(' ');
    });
}

let isEditMode = false;
let editUserId = null;
let editTaskType = null;

let allUsers = [];
let selectedUserIds = [];

document.addEventListener('DOMContentLoaded', function() {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Преподаватель';

    const urlParams = new URLSearchParams(window.location.search);
    editUserId = urlParams.get('userId');
    const userName = urlParams.get('userName');
    const userGroup = decodeURIComponent(urlParams.get('userGroup')) || '----------';
    editTaskType = urlParams.get('taskType') || 'min-max';
    isEditMode = !!editUserId;

    console.log('TaskEditPage: taskType=', editTaskType, 'isEditMode=', isEditMode); // Диагностика

    // Вызываем updateTaskInfo для немедленного отображения правильной задачи
    updateTaskInfo(editTaskType);

    if (isEditMode) {
        if (!editUserId || !userName || !userGroup) {
            alert('Не переданы необходимые параметры для редактирования');
            window.location.href = "/ProfileTeacherPage/ProfileTeacherPage.html";
            return;
        }

        const tableBody = document.querySelector('#admin-users-table tbody');
        tableBody.innerHTML = '';

        const tr = document.createElement('tr');
        tr.dataset.userId = editUserId;
        tr.innerHTML = `
            <td>
                <input type="checkbox" class="user-checkbox" data-user-id="${editUserId}" checked disabled>
                ${decodeURIComponent(userGroup)}
            </td>
            <td>${decodeURIComponent(userName)}</td>
        `;
        tableBody.appendChild(tr);

        selectedUserIds = [editUserId];
    } else {
        fetchDBData();
    }

    // Обработчик для кнопки выбора даты
    const datePickerButton = document.querySelector('.date-picker-button');
    const dueDateInput = document.querySelector('#due-date-input');
    if (datePickerButton && dueDateInput) {
        datePickerButton.addEventListener('click', () => {
            dueDateInput.showPicker(); // Программно открываем календарь
        });
    }
});

function updateTaskInfo(taskType) {
    console.log('updateTaskInfo: taskType=', taskType); // Диагностика
    const taskInfo = document.querySelector('.taskinfo');
    const taskSettings = document.querySelector('.tasksettings__content');
    const taskName = taskType === 'min-max' ? 'α & β отсечение' : 'Пятнашки A*';
    const taskDescription = taskType === 'min-max' ? 'Найти верный путь и отсекаемые узлы' : 'Решить головоломку Пятнашки с помощью A*';
    const taskImage = taskType === 'min-max' ? '/img/alpha-beta.webp' : '/img/FifteenPuzzle.webp';

    taskInfo.innerHTML = `
        <img class="taskinfo__img-task" src="${taskImage}" alt>
        <span class="taskinfo__taskname">${taskName}</span>
        <span class="taskinfo__task-description">${taskDescription}</span>
    `;

    taskSettings.innerHTML = `
        ${taskType === 'min-max' ? `
            <div class="tasksettings__input-group" style="display:none;">
                <span>Глубина:</span>
                <input type="number" id="tree-height-input" min="1" value="3">
            </div>
            <div class="tasksettings__input-group">
                <span>Шаблон:</span>
                <input type="number" id="template-input" min="1" max="4" value="1">
            </div>
            <div class="tasksettings__input-group">
                <span>Максимальное значение узла:</span>
                <input type="number" id="max-input" min="1" value="15">
            </div>
        ` : `
            <div class="tasksettings__input-group">
                <span>Размерность:</span>
                <input type="number" id="dimensions-input" min="3" max="4" value="3">
            </div>
            <div class="tasksettings__input-group">
                <span>Количество итераций:</span>
                <input type="number" id="iters-input" min="1" value="3">
            </div>
            <div class="tasksettings__input-group">
                <span>Эвристика:</span>
                <select class="tasksettings__input-group-dropdown" id="heuristic-input">
                    <option value="1">Расстояние Хемминга</option>
                    <option value="2">Манхэттенское расстояние</option>
                </select>
            </div>
        `}
    `;
}

document.getElementById('profile-tooltip__button-logout').addEventListener('click', e => {
    e.preventDefault();
    Logout();
});

async function Logout() {
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
}

async function fetchDBData() {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');

        const response = await fetch(`${apiHost}/Users`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
        });
        const data = await response.json();
        populateTable(data);
        return data;
    } catch (error) {
        console.error('Ошибка при получении данных:', error);
    }
    if (response.status === 401) {
        const refreshtoken = Cookies.get('RefreshToken');
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
    }
}

function populateTable(data) {
    const tableBody = document.querySelector('#admin-users-table tbody');
    tableBody.innerHTML = '';
    joinFullName(data);
    allUsers = data;

    data.forEach(row => {
        if (row.userName !== 'admin') {
            const group = row.group || '----------';
            const tr = document.createElement('tr');
            tr.dataset.userId = row.id;

            tr.innerHTML = `
                <td>
                    <input type="checkbox" class="user-checkbox" data-user-id="${row.id}">
                    ${group}
                </td>
                <td>${row.name}</td>
            `;

            tableBody.appendChild(tr);
        }
    });
    setupCheckboxes();
}

function setupCheckboxes() {
    document.getElementById('select-all-checkbox').addEventListener('change', function(e) {
        const checkboxes = document.querySelectorAll('.user-checkbox');
        checkboxes.forEach(checkbox => {
            checkbox.checked = e.target.checked;
        });
        updateSelectedUsers();
    });

    const checkboxes = document.querySelectorAll('.user-checkbox');
    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', updateSelectedUsers);
    });
}

function updateSelectedUsers() {
    selectedUserIds = [];
    const checkboxes = document.querySelectorAll('.user-checkbox:checked');
    checkboxes.forEach(checkbox => {
        selectedUserIds.push(checkbox.dataset.userId);
    });
}

document.querySelector('.admin-form__submit-button').addEventListener('click', async function(e) {
    e.preventDefault();

    if (selectedUserIds.length === 0) {
        alert('Пожалуйста, выберите хотя бы одного пользователя');
        return;
    }

    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');

        if (editTaskType === 'min-max') {
            const treeHeight = document.getElementById('tree-height-input').value;
            const max = document.getElementById('max-input').value;
            const template = document.getElementById('template-input').value;

            if (isEditMode) {
                const response = await fetch(`${apiHost}/AB/Users/${editUserId}?height=${parseInt(treeHeight)}&maxValue=${parseInt(max)}&template=${parseInt(template)}&generate=true`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${authtoken}`
                    }
                });

                if (response.ok) {
                    alert('Задание успешно обновлено');
                    window.location.href = "/ProfileTeacherPage/ProfileTeacherPage.html";
                } else {
                    throw new Error('Ошибка при обновлении задания');
                }
                if (response.status === 401) {
                    const refreshtoken = Cookies.get('RefreshToken');
                    if (isTokenExpired(authtoken)) {
                        refreshToken();
                    }
                }
            } else {
                const response = await fetch(`${apiHost}/AB/Users/Assign?treeHeight=${parseInt(treeHeight)}&max=${parseInt(max)}&template=${parseInt(template)}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${authtoken}`
                    },
                    body: JSON.stringify(selectedUserIds)
                });

                if (response.ok) {
                    alert('Задание успешно назначено');
                    clearCheckboxes();
                } else {
                    throw new Error('Ошибка при назначении задания');
                }
            }
        } else if (editTaskType === 'a-star') {
            const dimensions = document.getElementById('dimensions-input').value;
            const iters = document.getElementById('iters-input').value;
            const heuristic = document.getElementById('heuristic-input').value;

            if (isEditMode) {
                const response = await fetch(`${apiHost}/A/FifteenPuzzle/Users/${editUserId}?dimensions=${parseInt(dimensions)}&iters=${parseInt(iters)}&heuristic=${parseInt(heuristic)}`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${authtoken}`
                    }
                });

                if (response.ok) {
                    alert('Задание успешно обновлено');
                    window.location.href = "/ProfileTeacherPage/ProfileTeacherPage.html";
                } else {
                    throw new Error('Ошибка при обновлении задания');
                }
            } else {
                const response = await fetch(`${apiHost}/A/FifteenPuzzle/Users/Assign?dimensions=${parseInt(dimensions)}&iters=${parseInt(iters)}&heuristic=${parseInt(heuristic)}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${authtoken}`
                    },
                    body: JSON.stringify(selectedUserIds)
                });

                if (response.ok) {
                    alert('Задание успешно назначено');
                    clearCheckboxes();
                } else {
                    throw new Error('Ошибка при назначении задания');
                }
            }
        }
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Произошла ошибка: ' + error.message);
    }
});

function clearCheckboxes() {
    document.querySelectorAll('.user-checkbox').forEach(checkbox => {
        checkbox.checked = false;
    });

    document.getElementById('select-all-checkbox').checked = false;

    selectedUserIds = [];
}