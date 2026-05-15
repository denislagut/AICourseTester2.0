document.addEventListener('DOMContentLoaded', () => {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Преподаватель';

    document.getElementById('student-form').addEventListener('submit', loadStudentDashboard);
    document.getElementById('group-form').addEventListener('submit', loadGroupDashboard);
    document.getElementById('profile-tooltip__button-logout').addEventListener('click', logout);

    loadSummary();
});

async function authorizedGet(path) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const response = await fetch(`${apiHost}${path}`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.status === 401) {
        const refreshedToken = await refreshToken();
        if (refreshedToken) {
            return authorizedGet(path);
        }
    }

    if (!response.ok) {
        throw new Error(`Ошибка запроса: ${response.status}`);
    }

    return response.json();
}

async function loadSummary() {
    const status = document.getElementById('summary-status');
    const grid = document.getElementById('summary-grid');

    status.textContent = 'Загрузка...';
    status.classList.remove('error');
    grid.innerHTML = '';

    try {
        const summary = await authorizedGet('/Analytics/Summary');
        renderSummary(summary);
        status.textContent = '';
    } catch (error) {
        status.textContent = error.message;
        status.classList.add('error');
    }
}

async function loadStudentDashboard(event) {
    event.preventDefault();

    const userId = document.getElementById('student-user-id').value.trim();
    const status = document.getElementById('student-status');
    const content = document.getElementById('student-content');

    if (!userId) {
        setError(status, 'Введите идентификатор пользователя');
        return;
    }

    setLoading(status, content);

    try {
        const [analytics, recommendations] = await Promise.all([
            authorizedGet(`/Analytics/Students/${encodeURIComponent(userId)}`),
            authorizedGet(`/Recommendations/Students/${encodeURIComponent(userId)}`)
        ]);

        renderStudentAnalytics(analytics, recommendations);
        status.textContent = '';
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function loadGroupDashboard(event) {
    event.preventDefault();

    const groupId = document.getElementById('group-id').value.trim();
    const status = document.getElementById('group-status');
    const content = document.getElementById('group-content');

    if (!groupId) {
        setError(status, 'Введите идентификатор группы');
        return;
    }

    setLoading(status, content);

    try {
        const [analytics, recommendations] = await Promise.all([
            authorizedGet(`/Analytics/Groups/${encodeURIComponent(groupId)}`),
            authorizedGet(`/Recommendations/Groups/${encodeURIComponent(groupId)}`)
        ]);

        renderGroupAnalytics(analytics, recommendations);
        status.textContent = '';
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

function renderSummary(summary) {
    const metrics = [
        ['Студенты', summary.totalStudents],
        ['Группы', summary.totalGroups],
        ['Количество ошибок', summary.totalErrors],
        ['Количество пробелов', summary.totalKnowledgeGaps],
        ['Средний показатель пробела', summary.averageGapScore],
        ['Серьёзные ошибки', summary.highSeverityErrorsCount]
    ];

    document.getElementById('summary-grid').innerHTML = metrics
        .map(([label, value]) => `
            <article class="metric-card">
                <span class="metric-card__label">${escapeHtml(label)}</span>
                <strong class="metric-card__value">${formatValue(value)}</strong>
            </article>
        `)
        .join('');
}

function renderStudentAnalytics(analytics, recommendations) {
    const content = document.getElementById('student-content');
    content.innerHTML = `
        ${renderPanel('Статистика студента', renderDetails([
            ['Идентификатор пользователя', analytics.userId],
            ['Логин', analytics.userName],
            ['ФИО', analytics.fullName],
            ['Группа', formatGroup(analytics.groupId, analytics.groupName)],
            ['Количество ошибок', analytics.totalErrors],
            ['Количество пробелов', analytics.totalKnowledgeGaps],
            ['Средний показатель пробела', analytics.averageGapScore],
            ['Серьёзные ошибки', analytics.highSeverityErrorsCount]
        ]))}
        ${renderPanel('Пробелы в знаниях', renderKnowledgeGaps(analytics.topKnowledgeGaps))}
        ${renderPanel('Рекомендации', renderRecommendations(recommendations))}
    `;
}

function renderGroupAnalytics(analytics, recommendations) {
    const content = document.getElementById('group-content');
    content.innerHTML = `
        ${renderPanel('Статистика группы', renderDetails([
            ['Идентификатор группы', analytics.groupId],
            ['Название группы', analytics.groupName],
            ['Количество студентов', analytics.studentsCount],
            ['Количество ошибок', analytics.totalErrors],
            ['Количество пробелов', analytics.totalKnowledgeGaps],
            ['Средний показатель пробела', analytics.averageGapScore],
            ['Серьёзные ошибки', analytics.highSeverityErrorsCount]
        ]))}
        ${renderPanel('Рекомендации группы', renderRecommendations(recommendations))}
    `;
}

function renderPanel(title, body) {
    return `
        <section class="panel">
            <h3>${escapeHtml(title)}</h3>
            ${body}
        </section>
    `;
}

function renderDetails(rows) {
    return `
        <div class="details-list">
            ${rows.map(([label, value]) => `
                <div>
                    <span>${escapeHtml(label)}</span>
                    <span>${escapeHtml(formatValue(value))}</span>
                </div>
            `).join('')}
        </div>
    `;
}

function renderKnowledgeGaps(gaps) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return '<p class="empty-state">Пробелы в знаниях не найдены.</p>';
    }

    return `
        <div class="gap-list">
            ${gaps.map(gap => `
                <article class="gap-row">
                    <div class="gap-row__title">${escapeHtml(gap.aspectName || `Аспект #${gap.knowledgeAspectId}`)}</div>
                    <div class="gap-row__meta">
                        Тема: ${escapeHtml(gap.topicName || 'не указана')}<br>
                        Количество: ${formatValue(gap.count)} | Средний показатель: ${formatValue(gap.averageGapScore)} | Максимальный показатель: ${formatValue(gap.maxGapScore)}
                    </div>
                </article>
            `).join('')}
        </div>
    `;
}

function renderRecommendations(recommendations) {
    if (!Array.isArray(recommendations) || recommendations.length === 0) {
        return '<p class="empty-state">Рекомендации не найдены.</p>';
    }

    return `
        <div class="recommendation-list">
            ${recommendations.map(item => `
                <article class="recommendation-item">
                    <div class="recommendation-item__title">
                        <span class="priority ${priorityClass(item.priority)}">${escapeHtml(translatePriority(item.priority))}</span>
                        ${escapeHtml(item.title || item.aspectName || 'Рекомендация')}
                    </div>
                    <div class="recommendation-item__meta">
                        Аспект: ${escapeHtml(item.aspectName || `#${item.knowledgeAspectId}`)} |
                        Тема: ${escapeHtml(item.topicName || 'не указана')} |
                        Показатель пробела: ${formatValue(item.gapScore)} |
                        Ошибки: ${formatValue(item.relatedErrorCount)}
                        ${item.affectedStudentsCount == null ? '' : ` | Затронуто студентов: ${formatValue(item.affectedStudentsCount)}`}
                    </div>
                    <p class="recommendation-item__description">${escapeHtml(item.description || '')}</p>
                </article>
            `).join('')}
        </div>
    `;
}

function setLoading(status, content) {
    status.textContent = 'Загрузка...';
    status.classList.remove('error');
    content.innerHTML = '';
}

function setError(status, message) {
    status.textContent = message;
    status.classList.add('error');
}

function formatGroup(groupId, groupName) {
    if (groupId == null && !groupName) {
        return 'не назначена';
    }

    return `${groupName || 'Группа'} (${groupId || '-'})`;
}

function formatValue(value) {
    if (value === null || value === undefined || value === '') {
        return '-';
    }

    return String(value);
}

function priorityClass(priority) {
    const normalized = String(priority || '').toLowerCase();

    if (normalized === 'high') return 'priority--high';
    if (normalized === 'medium') return 'priority--medium';
    return 'priority--low';
}

function translatePriority(priority) {
    const normalized = String(priority || '').toLowerCase();

    if (normalized === 'high') return 'Высокий';
    if (normalized === 'medium') return 'Средний';
    return 'Низкий';
}

async function logout(event) {
    event.preventDefault();

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
            return;
        }

        if (response.status === 401 && isTokenExpired(authtoken)) {
            await refreshToken();
        }

        alert('Не удалось выйти из системы');
    } catch (error) {
        console.error('Ошибка при выходе:', error);
        alert('Произошла ошибка при выходе');
    }
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
