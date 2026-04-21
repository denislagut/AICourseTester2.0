document.addEventListener('DOMContentLoaded', function () {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    
    fetchDBData();
    setupGroupDropdowns().then(() => {
        setupGroupButtons();
    });
});

function splitFullName(FullName, separator) {
    let SplittedFullName = FullName.split(separator);
    return SplittedFullName;
}

function joinFullName(data) {
    data.forEach(row => {
        const nameParts = [];
        if (row.secondName) nameParts.push(row.secondName);
        if (row.name) nameParts.push(row.name);
        if (row.patronymic) nameParts.push(row.patronymic);
        
        row.name = nameParts.join(' ');
        row.group = row.group || '----------';
    });
}

function generateLogin(fullName) {
    const translitRules = {
        'а': 'a', 'б': 'b', 'в': 'v', 'г': 'g', 'д': 'd', 'е': 'e', 'ё': 'yo', 'ж': 'zh',
        'з': 'z', 'и': 'i', 'й': 'y', 'к': 'k', 'л': 'l', 'м': 'm', 'н': 'n', 'о': 'o',
        'п': 'p', 'р': 'r', 'с': 's', 'т': 't', 'у': 'u', 'ф': 'f', 'х': 'h', 'ц': 'ts',
        'ч': 'ch', 'ш': 'sh', 'щ': 'sch', 'ъ': '', 'ы': 'y', 'ь': '', 'э': 'e', 'ю': 'yu',
        'я': 'ya',
        'А': 'A', 'Б': 'B', 'В': 'V', 'Г': 'G', 'Д': 'D', 'Е': 'E', 'Ё': 'Yo', 'Ж': 'Zh',
        'З': 'Z', 'И': 'I', 'Й': 'Y', 'К': 'K', 'Л': 'L', 'М': 'M', 'Н': 'N', 'О': 'O',
        'П': 'P', 'Р': 'R', 'С': 'S', 'Т': 'T', 'У': 'U', 'Ф': 'F', 'Х': 'H', 'Ц': 'Ts',
        'Ч': 'Ch', 'Ш': 'Sh', 'Щ': 'Sch', 'Ъ': '', 'Ы': 'Y', 'Ь': '', 'Э': 'E', 'Ю': 'Yu',
        'Я': 'Ya'
    };

    let transliterated = '';
    for (const char of fullName) {
        transliterated += translitRules[char] || char;
    }

    const [lastName, firstName, middleName] = transliterated.split(' ').filter(Boolean);
    let baseLogin = '';

    if (lastName && firstName && middleName) {
        baseLogin = lastName.toLowerCase() + firstName[0].toLowerCase() + middleName[0].toLowerCase();
    } else if (firstName && middleName) {
        baseLogin = firstName.toLowerCase() + '.' + middleName.toLowerCase();
    } else if (lastName && firstName) {
        baseLogin = lastName.toLowerCase() + firstName[0].toLowerCase();
    } else if (lastName) {
        baseLogin = lastName.toLowerCase();
    } else {
        baseLogin = 'user' + Math.floor(Math.random() * 100);
    }

    const randomDigit = Math.floor(Math.random() * 10);
    baseLogin += randomDigit;

    const specialChars = '!@#$%^&*';
    const randomSpecialChar = specialChars[Math.floor(Math.random() * specialChars.length)];
    baseLogin += randomSpecialChar;

    return baseLogin;
}

function generatePassword(length = 8) {
    if (length < 8) length = 8;

    const groups = [
        'ABCDEFGHIJKLMNOPQRSTUVWXYZ',
        'abcdefghijklmnopqrstuvwxyz',
        '0123456789',
        '!@#$%^&*'
    ];

    let password = groups.map(group => group[Math.floor(Math.random() * group.length)]);

    const allChars = groups.join('');
    for (let i = 0; i < length - groups.length; i++) {
        password.push(allChars[Math.floor(Math.random() * allChars.length)]);
    }

    password = password.sort(() => Math.random() - 0.5).join('');
    return password;
}

async function checkIfUserExists(userId) {
    if (!userId) return false;

    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');

        const response = await fetch(`${apiHost}/Users/${userId}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        return response.status === 200;
    } catch (error) {
        console.error('Ошибка проверки существования пользователя:', error);
        return false;
    }
}

async function checkIfGroupExists(groupName) {
    const groups = await fetchGroups();
    return groups.some(group => group.name === groupName);
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
    if (response.status === 401) {
        const refreshtoken = Cookies.get('RefreshToken');
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
    }
    if (response.status === 200) {
        sessionStorage.removeItem('userFullName');
        window.location.href = "/LoginPage/LoginPage.html";
    }
}

document.getElementById('id_button_generate_login_password').addEventListener('click', e => {
    e.preventDefault();
    document.getElementById('id_userName').value = generateLogin(document.getElementById('id_userFullName').value);
    document.getElementById('id_userPassword').value = generatePassword();
});

document.getElementById('id_button_admin_save').addEventListener('click', async e => {
    e.preventDefault();

    const from_register_user = document.getElementById('id_form_register_user');
    const from_register_user_styles = window.getComputedStyle(from_register_user);
    const id_groupId = document.getElementById('id_groupId').value;
    const userId = document.getElementById('id_userId').value;
    const userFullName = document.getElementById('id_userFullName').value.trim();
    const userName = document.getElementById('id_userName').value.trim();
    const userPassword = document.getElementById('id_userPassword').value.trim();

    if (from_register_user_styles.display !== 'none') {
        // Validate required fields
        if (!id_groupId) {
            alert('Пожалуйста, выберите группу');
            return;
        }
        if (!userFullName) {
            alert('Пожалуйста, введите ФИО');
            return;
        }
        if (!userName) {
            alert('Пожалуйста, введите логин');
            return;
        }
        if (!userPassword) {
            alert('Пожалуйста, введите пароль');
            return;
        }

        const SplittedFullName = splitFullName(userFullName, " ");

        const data = {
            userId: userId,
            userName: userName,
            password: userPassword,
            name: SplittedFullName[1] || "",
            secondName: SplittedFullName[0] || "",
            patronymic: SplittedFullName[2] || "-",
            groupId: id_groupId
        };

        if (await checkIfUserExists(userId)) {
            putUser(data);
            document.getElementById('id_userId').value = '';
            document.getElementById('id_groupId').value = '';
        } else {
            delete data.userId;
            addUser(data);
            document.getElementById('id_userId').value = '';
            document.getElementById('id_groupId').value = '';
        }
    } else {
        const groupName = document.getElementById('id_groupName').value.trim();
        if (!groupName) {
            alert('Введите название группы');
            return;
        }
        
        if (await checkIfGroupExists(groupName)) {
            alert('Группа с таким названием уже существует');
            return;
        }
        
        const data = { groupName };
        addGroup(data);
    }
});

async function addUser(data) {
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');

    try {
        const response = await fetch(`${apiHost}/Users/Register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
            body: JSON.stringify(data)
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        if (response.status === 200) {
            alert('Пользователь добавлен');
            document.getElementById("id_groupName-of-user").value = '';
            fetchDBData();
        } else if (response.status === 400) {
            const errorData = await response.json();
            alert(`Ошибка: Пользователь с логином "${data.userName}" уже существует`);
        } else {
            alert('Ошибка при добавлении пользователя');
        }
    } catch (error) {
        console.error('Ошибка при добавлении пользователя:', error);
        alert('Произошла ошибка при добавлении пользователя');
    }
}

async function addGroup(groupName) {
    if (!groupName || !groupName.trim()) {
        alert('Введите название группы');
        return false;
    }

    const groups = await fetchGroups();
    if (groups.some(group => group.name === groupName)) {
        alert('Группа с таким названием уже существует');
        return false;
    }

    const authtoken = Cookies.get('.AspNetCore.Identity.Application');

    const response = await fetch(`${apiHost}/Users/Groups?groupName=` + groupName, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        },
    });
    if (response.status === 401) {
        const refreshtoken = Cookies.get('RefreshToken');
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
    }
    if (response.ok) {
        document.getElementById("id_groupName-of-user").value = '';
        alert('Группа успешно добавлена');
        await Promise.all([fetchDBData(), setupGroupDropdowns()]);
        return true;
    } else {
        const errorData = await response.json();
        alert(errorData.message || 'Ошибка при добавлении группы');
        return false;
    }
}

async function deleteGroup(groupId) {
    if (!groupId) {
        alert('Группа не выбрана');
        return false;
    }

    if (!confirm('Вы уверены, что хотите удалить эту группу?')) {
        return false;
    }

    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const response = await fetch(`${apiHost}/Users/Groups?id=${groupId}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (response.ok) {
            alert('Группа успешно удалена');
            await Promise.all([fetchDBData(), setupGroupDropdowns()]);
            return true;
        } else {
            alert('Ошибка при удалении группы');
            return false;
        }
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Произошла ошибка при удалении группы');
        return false;
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
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        const data = await response.json();
        populateTable(data);
        return data;
    } catch (error) {
        console.error('Ошибка при получении данных:', error);
    }
}

function populateTable(data) {
    const tableBody = document.querySelector('#admin-users-table tbody');
    tableBody.innerHTML = '';
    joinFullName(data);

    data.forEach(row => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${row.group}</td>
            <td>${row.name}</td>
            <td class="actions-cell">
                <button class="button-edit" id="id_admin-list__button-edit" data-id="${row.id}" title="Редактировать"></button>
                <button class="button-delete" id="id_admin-list__button-delete" data-id="${row.id}" title="Удалить"></button>
            </td>
        `;
        tableBody.appendChild(tr);
    });

    document.querySelectorAll('#id_admin-list__button-delete').forEach(btn => {
        btn.addEventListener('click', async function () {
            if (confirm('Вы уверены, что хотите удалить эту запись?')) {
                await deleteRecord(this.getAttribute('data-id'));
            }
        });
    });

    document.querySelectorAll('#id_admin-list__button-edit').forEach(btn => {
        btn.addEventListener('click', async function () {
            await editRecord(this.getAttribute('data-id'));
        });
    });
}

async function deleteRecord(id) {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const response = await fetch(`${apiHost}/Users?userId=` + id, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`,
            }
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        alert('Запись успешно удалена');
        fetchDBData();
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Не удалось удалить запись');
    }
}

async function editRecord(id) {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const response = await fetch(`${apiHost}/Users/${id}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        var userdata = await response.json();
    } catch (error) {
        console.error('Ошибка при получении данных:', error);
    }
    document.getElementById('id_groupName-of-user').value = userdata.group || '----------';
    document.getElementById('id_userFullName').value = [userdata.secondName, userdata.name, userdata.patronymic].join(' ');
    document.getElementById('id_userId').value = userdata.id;
    document.getElementById('id_groupId').value = userdata.groupId;
}

async function putUser(data) {
    var form_data = new FormData();
    for (var key in data) {
        if (key !== 'userId') {
            form_data.append(key, data[key]);
        }
    }

    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');

        const response = await fetch(`${apiHost}/Users?userId=${data.userId}`, {
            method: 'PUT',
            headers: {
                Authorization: `Bearer ${authtoken}`
            },
            body: form_data
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        if (response.status == 200) {
            alert('Запись успешно изменена');
        } else {
            alert('Ошибка при обновлении пользователя');
        }
        fetchDBData();
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Произошла ошибка при обновлении пользователя');
    }
}

async function fetchGroups() {
    try {
        const authtoken = Cookies.get('.AspNetCore.Identity.Application');
        const response = await fetch(`${apiHost}/Users/Groups`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
        });
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        return await response.json();
    } catch (error) {
        console.error('Ошибка при получении списка групп:', error);
        return [];
    }
}

async function setupGroupDropdowns() {
    const groups = await fetchGroups();
    
    document.querySelectorAll('.profile-tooltip__button-chevron').forEach(btn => {
        btn.replaceWith(btn.cloneNode(true));
    });

    document.body.addEventListener('click', function(e) {
        if (e.target.closest('#student-tab ~ .tabs__content .profile-tooltip__button-chevron')) {
            e.preventDefault();
            toggleGroupDropdown(e.target, groups, 'id_groupName-of-user', 'id_groupId');
        }
        
        if (e.target.closest('#group-tab ~ .tabs__content .profile-tooltip__button-chevron')) {
            e.preventDefault();
            toggleGroupDropdown(e.target, groups, 'id_groupName');
        }
    });
}

function toggleGroupDropdown(button, groups, inputNameId, inputGroupId = null) {
    document.querySelectorAll('.group-dropdown').forEach(d => d.remove());

    if (button.classList.contains('dropdown-open')) {
        button.classList.remove('dropdown-open');
        return;
    }

    button.classList.add('dropdown-open');

    const dropdown = document.createElement('div');
    dropdown.className = 'group-dropdown';

    groups.forEach(group => {
        const item = document.createElement('div');
        item.className = 'group-dropdown-item';
        item.textContent = group.name;

        item.addEventListener('click', () => {
            document.getElementById(inputNameId).value = group.name;
            if (inputGroupId && document.getElementById(inputGroupId)) {
                document.getElementById(inputGroupId).value = group.id;
            }
            dropdown.remove();
            button.classList.remove('dropdown-open');
        });

        dropdown.appendChild(item);
    });

    button.parentNode.appendChild(dropdown);

    const rect = button.getBoundingClientRect();
    const parentRect = button.parentNode.getBoundingClientRect();
    const scrollTop = window.scrollY || document.documentElement.scrollTop;

    Object.assign(dropdown.style, {
        position: 'absolute',
        top: `${rect.bottom - parentRect.top + scrollTop + 20}px`,
        left: `${rect.left - parentRect.left}px`,
        zIndex: '1000',
        minWidth: `${rect.width}px`
    });

    const closeHandler = (e) => {
        if (!dropdown.contains(e.target) && e.target !== button) {
            dropdown.remove();
            button.classList.remove('dropdown-open');
            document.removeEventListener('click', closeHandler);
        }
    };

    setTimeout(() => document.addEventListener('click', closeHandler), 0);
}

function setupGroupButtons() {
    document.querySelector('.profile-tooltip__button-add-group')?.addEventListener('click', async function(e) {
        e.preventDefault();
        const groupNameInput = document.getElementById('id_groupName-of-user');
        const groupName = groupNameInput.value.trim();
        
        if (!groupName) {
            alert('Введите название группы в поле ввода');
            return;
        }

        const success = await addGroup(groupName);
        if (success) {
            await setupGroupDropdowns();
            document.getElementById('id_groupId').value = '';
        }
    });

    document.querySelector('.profile-tooltip__button-delete-group')?.addEventListener('click', async function(e) {
        e.preventDefault();
        const groupId = document.getElementById('id_groupId').value;
        const groupName = document.getElementById('id_groupName-of-user').value;
        
        if (!groupId || !groupName) {
            alert('Сначала выберите группу из списка');
            return;
        }

        const success = await deleteGroup(groupId);
        if (success) {
            await setupGroupDropdowns();
            document.getElementById('id_groupName-of-user').value = '';
            document.getElementById('id_groupId').value = '';
        }
    });
}