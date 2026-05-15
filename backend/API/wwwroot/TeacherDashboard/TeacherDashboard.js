document.addEventListener('DOMContentLoaded', () => {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Преподаватель';

    document.getElementById('student-form').addEventListener('submit', loadStudentDashboard);
    document.getElementById('group-form').addEventListener('submit', loadGroupDashboard);
    document.getElementById('student-report-form').addEventListener('submit', generateStudentReport);
    document.getElementById('group-report-form').addEventListener('submit', generateGroupReport);
    document.getElementById('student-report-history-button').addEventListener('click', loadStudentReportHistory);
    document.getElementById('group-report-history-button').addEventListener('click', loadGroupReportHistory);
    document.getElementById('reports-content').addEventListener('click', toggleReportDetails);
    document.getElementById('reports-content').addEventListener('click', downloadReportFile);
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

async function authorizedPost(path) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const response = await fetch(`${apiHost}${path}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.status === 401) {
        const refreshedToken = await refreshToken();
        if (refreshedToken) {
            return authorizedPost(path);
        }
    }

    if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response));
    }

    return response.json();
}

async function getResponseErrorMessage(response) {
    if (response.status === 404) {
        return 'Пользователь или группа не найдены';
    }

    const text = await response.text();

    if (text) {
        return text.replace(/^"|"$/g, '');
    }

    return `Ошибка запроса: ${response.status}`;
}

async function authorizedGetWithMessage(path) {
    try {
        return await authorizedGet(path);
    } catch (error) {
        const statusMatch = String(error.message).match(/(\d{3})$/);
        if (statusMatch && statusMatch[1] === '404') {
            throw new Error('Пользователь или группа не найдены');
        }

        throw error;
    }
}

async function authorizedGetBlob(path) {
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
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.status === 401) {
        const refreshedToken = await refreshToken();
        if (refreshedToken) {
            return authorizedGetBlob(path);
        }
    }

    if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response));
    }

    return response.blob();
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

async function generateStudentReport(event) {
    event.preventDefault();

    const userId = document.getElementById('report-student-user-id').value.trim();
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!userId) {
        setError(status, 'Введите идентификатор пользователя');
        return;
    }

    setLoading(status, content);

    try {
        const report = await authorizedPost(`/Reports/Students/${encodeURIComponent(userId)}/Generate`);
        status.textContent = 'Отчёт по студенту успешно сформирован.';
        status.classList.remove('error');
        content.innerHTML = renderReports([report]);
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function generateGroupReport(event) {
    event.preventDefault();

    const groupId = document.getElementById('report-group-id').value.trim();
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!groupId) {
        setError(status, 'Введите идентификатор группы');
        return;
    }

    setLoading(status, content);

    try {
        const report = await authorizedPost(`/Reports/Groups/${encodeURIComponent(groupId)}/Generate`);
        status.textContent = 'Отчёт по группе успешно сформирован.';
        status.classList.remove('error');
        content.innerHTML = renderReports([report]);
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function loadStudentReportHistory() {
    const userId = document.getElementById('report-student-user-id').value.trim();
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!userId) {
        setError(status, 'Введите идентификатор пользователя');
        return;
    }

    setLoading(status, content);

    try {
        const reports = await authorizedGetWithMessage(`/Reports/Students/${encodeURIComponent(userId)}/History`);
        status.textContent = '';
        content.innerHTML = renderReports(reports);
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function loadGroupReportHistory() {
    const groupId = document.getElementById('report-group-id').value.trim();
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!groupId) {
        setError(status, 'Введите идентификатор группы');
        return;
    }

    setLoading(status, content);

    try {
        const reports = await authorizedGetWithMessage(`/Reports/Groups/${encodeURIComponent(groupId)}/History`);
        status.textContent = '';
        content.innerHTML = renderReports(reports);
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

function renderGapScoreIndicator(score) {
    const value = clampPercent(Number(score) || 0);

    return `
        <div class="score-indicator">
            <div class="score-indicator__top">
                <span>Средний уровень пробелов</span>
                <strong>${formatValue(score)}%</strong>
            </div>
            <div class="score-indicator__track">
                <div class="score-indicator__bar ${scoreLevelClass(value)}" style="width: ${value}%"></div>
            </div>
        </div>
    `;
}

function renderRiskIndicator(level) {
    const normalized = String(level || '').toLowerCase();
    const value = normalized === 'high' ? 100 : normalized === 'medium' ? 65 : normalized === 'low' ? 35 : 0;

    return `
        <div class="score-indicator risk-indicator">
            <div class="score-indicator__top">
                <span>Уровень риска</span>
                <strong>${escapeHtml(translateRiskLevel(level))}</strong>
            </div>
            <div class="score-indicator__track">
                <div class="score-indicator__bar ${priorityClass(level)}" style="width: ${value}%"></div>
            </div>
        </div>
    `;
}

function renderKnowledgeGapChart(gaps) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return '<p class="empty-state">Нет данных для диаграммы проблемных тем.</p>';
    }

    const items = gaps.map(gap => ({
        label: gap.AspectName || gap.aspectName || `Аспект #${gap.KnowledgeAspectId || gap.knowledgeAspectId}`,
        meta: gap.TopicName || gap.topicName || 'тема не указана',
        value: Number(gap.AverageGapScore ?? gap.averageGapScore ?? gap.MaxGapScore ?? gap.maxGapScore ?? 0),
        valueLabel: `${formatValue(gap.AverageGapScore ?? gap.averageGapScore)}%`
    }));

    return renderBarChart(items, 'Пробел');
}

function renderErrorTypeChart(errorTypes) {
    if (!Array.isArray(errorTypes) || errorTypes.length === 0) {
        return '<p class="empty-state">Нет данных для диаграммы типов ошибок.</p>';
    }

    const maxCount = Math.max(...errorTypes.map(item => Number(item.Count ?? item.count ?? 0)), 1);
    const items = errorTypes.map(errorType => {
        const count = Number(errorType.Count ?? errorType.count ?? 0);

        return {
            label: errorType.Name || errorType.name || errorType.Code || errorType.code || `Ошибка #${errorType.ErrorTypeId || errorType.errorTypeId}`,
            meta: `Средняя severity: ${formatValue(errorType.AverageSeverity ?? errorType.averageSeverity)}`,
            value: (count / maxCount) * 100,
            valueLabel: `${formatValue(count)}`
        };
    });

    return renderBarChart(items, 'Ошибки');
}

function renderBarChart(items, valueTitle) {
    return `
        <div class="bar-chart">
            ${items.map(item => {
                const width = clampPercent(item.value);

                return `
                    <article class="bar-chart__row">
                        <div class="bar-chart__labels">
                            <span>${escapeHtml(item.label)}</span>
                            <strong>${escapeHtml(valueTitle)}: ${escapeHtml(item.valueLabel)}</strong>
                        </div>
                        <div class="bar-chart__meta">${escapeHtml(item.meta || '')}</div>
                        <div class="bar-chart__track">
                            <div class="bar-chart__bar ${scoreLevelClass(width)}" style="width: ${width}%"></div>
                        </div>
                    </article>
                `;
            }).join('')}
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

function renderReports(reports) {
    if (!Array.isArray(reports) || reports.length === 0) {
        return '<p class="empty-state">Отчёты пока не сформированы.</p>';
    }

    return `
        <div class="report-list">
            ${reports.map(report => {
                const detailsId = `report-details-${report.id}`;
                const summary = parseJson(report.summaryJson);
                const analytics = parseJson(report.analyticsJson);
                const recommendations = parseJson(report.recommendationsJson) || [];

                return `
                    <article class="report-card">
                        <div class="report-card__title">${escapeHtml(report.title || 'Отчёт')}</div>
                        ${renderReportSummary(report, summary)}
                        <div class="report-card__actions">
                            <button class="report-details-toggle" type="button" data-details-id="${escapeHtml(detailsId)}">Показать детали</button>
                            <button class="report-download-button" type="button" data-report-id="${escapeHtml(report.id)}" data-export-format="Pdf">Скачать PDF</button>
                            <button class="report-download-button" type="button" data-report-id="${escapeHtml(report.id)}" data-export-format="Excel">Скачать Excel</button>
                        </div>
                        <div id="${escapeHtml(detailsId)}" class="report-card__details">
                            ${renderPedagogicalSummary(summary)}
                            ${renderReportAnalytics(analytics)}
                            ${renderReportRecommendations(recommendations)}
                        </div>
                    </article>
                `;
            }).join('')}
        </div>
    `;
}

function renderReportSummary(report, summary) {
    const rows = [
        ['Id', report.id],
        ['Тип отчёта', translateReportType(summary?.reportType || report.reportType)],
        ['Студент/группа', summary?.userId || summary?.groupId || report.userId || report.groupId],
        ['Количество рекомендаций', summary?.recommendationsCount ?? '-'],
        ['Уровень риска', translateRiskLevel(summary?.riskLevel)],
        ['Формат', report.format],
        ['Дата создания', formatDate(summary?.createdAt || report.createdAt)]
    ];

    return `
        <div class="report-summary">
            ${renderDetails(rows)}
        </div>
    `;
}

function renderPedagogicalSummary(summary) {
    if (!summary) {
        return '';
    }

    return `
        <div class="report-block report-pedagogical-block">
            <h4>Краткий вывод</h4>
            <p class="report-conclusion">${escapeHtml(summary.conclusion || 'Вывод будет сформирован после обновления отчёта.')}</p>
        </div>
        <div class="report-block">
            <h4>Уровень риска</h4>
            <span class="priority ${priorityClass(summary.riskLevel)}">${escapeHtml(translateRiskLevel(summary.riskLevel))}</span>
            ${renderRiskIndicator(summary.riskLevel)}
        </div>
        <div class="report-block">
            <h4>Рекомендуемые действия преподавателя</h4>
            ${renderTeacherActions(summary.teacherActions || [])}
        </div>
    `;
}

function renderMainProblems(problems) {
    if (!Array.isArray(problems) || problems.length === 0) {
        return '<p class="empty-state">Основные проблемы не выделены.</p>';
    }

    return `
        <div class="report-problem-list">
            ${problems.map(problem => `
                <article class="report-problem-card">
                    <div class="gap-row__title">${escapeHtml(problem.aspectName || problem.AspectName || 'Аспект не указан')}</div>
                    <div class="gap-row__meta">
                        Тема: ${escapeHtml(problem.topicName || problem.TopicName || 'не указана')}<br>
                        Показатель: ${formatValue(problem.score ?? problem.Score)} |
                        Уровень: ${escapeHtml(translateRiskLevel(problem.level || problem.Level))}
                    </div>
                    <p class="report-problem-card__explanation">${escapeHtml(problem.explanation || problem.Explanation || '')}</p>
                </article>
            `).join('')}
        </div>
    `;
}

function renderTeacherActions(actions) {
    if (!Array.isArray(actions) || actions.length === 0) {
        return '<p class="empty-state">Дополнительные действия не требуются.</p>';
    }

    return `
        <ul class="teacher-actions-list">
            ${actions.map(action => `<li>${escapeHtml(action)}</li>`).join('')}
        </ul>
    `;
}

function renderReportAnalytics(analytics) {
    if (!analytics) {
        return '<p class="empty-state">Данные аналитики не найдены.</p>';
    }

    const topGaps = parseJson(analytics.TopKnowledgeGapsJson || analytics.topKnowledgeGapsJson) || [];
    const topErrors = parseJson(analytics.TopErrorTypesJson || analytics.topErrorTypesJson) || [];

    return `
        <div class="report-block">
            ${renderKnowledgeProblemPieChart(topGaps)}
        </div>
        <div class="report-block">
            <h4>Общая статистика</h4>
            <div class="report-metrics">
                ${renderReportMetric('Количество ошибок', analytics.TotalErrors ?? analytics.totalErrors)}
                ${renderReportMetric('Количество пробелов знаний', analytics.TotalKnowledgeGaps ?? analytics.totalKnowledgeGaps)}
                ${renderReportMetric('Средний уровень пробелов (%)', analytics.AverageGapScore ?? analytics.averageGapScore)}
                ${renderReportMetric('Количество критических ошибок', analytics.HighSeverityErrorsCount ?? analytics.highSeverityErrorsCount)}
            </div>
            ${renderGapScoreIndicator(analytics.AverageGapScore ?? analytics.averageGapScore)}
        </div>
        <div class="report-block">
            <h4>Проблемные темы</h4>
            ${renderKnowledgeGapChart(topGaps)}
        </div>
        <div class="report-block">
            <h4>Основные типы ошибок</h4>
            ${renderErrorTypeChart(topErrors)}
        </div>
    `;
}

function renderReportMetric(label, value) {
    return `
        <article class="metric-card report-metric-card">
            <span class="metric-card__label">${escapeHtml(label)}</span>
            <strong class="metric-card__value">${formatValue(value)}</strong>
        </article>
    `;
}

function renderKnowledgeProblemPieChart(gaps) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return `
            <div class="pie-chart-card">
                <h4>Распределение уровня проблемности знаний</h4>
                <p class="empty-state">Недостаточно данных для построения диаграммы</p>
            </div>
        `;
    }

    const segments = [
        { key: 'critical', label: 'Критические', color: '#D92D20', count: 0 },
        { key: 'medium', label: 'Средние', color: '#F79009', count: 0 },
        { key: 'low', label: 'Низкие', color: '#12B76A', count: 0 }
    ];

    gaps.forEach(gap => {
        const score = Number(gap.AverageGapScore ?? gap.averageGapScore ?? 0);

        if (score > 75) {
            segments[0].count += 1;
        } else if (score >= 50) {
            segments[1].count += 1;
        } else {
            segments[2].count += 1;
        }
    });

    const total = segments.reduce((sum, item) => sum + item.count, 0);

    if (total === 0) {
        return `
            <div class="pie-chart-card">
                <h4>Распределение уровня проблемности знаний</h4>
                <p class="empty-state">Недостаточно данных для построения диаграммы</p>
            </div>
        `;
    }

    let cursor = 0;
    const gradientParts = segments.map(segment => {
        const percent = (segment.count / total) * 100;
        const start = cursor;
        cursor += percent;
        return `${segment.color} ${start}% ${cursor}%`;
    });

    return `
        <div class="pie-chart-card">
            <h4>Распределение уровня проблемности знаний</h4>
            <div class="pie-chart-layout">
                <div class="pie-chart" style="background: conic-gradient(${gradientParts.join(', ')});">
                    <div class="pie-chart__center">
                        <strong>${total}</strong>
                        <span>тем</span>
                    </div>
                </div>
                <div class="pie-chart-legend">
                    ${segments.map(segment => {
                        const percent = Math.round((segment.count / total) * 100);

                        return `
                            <div class="pie-chart-legend__item">
                                <span class="pie-chart-legend__dot" style="background: ${segment.color};"></span>
                                <span>${escapeHtml(segment.label)} — ${percent}% (${segment.count})</span>
                            </div>
                        `;
                    }).join('')}
                </div>
            </div>
        </div>
    `;
}

function renderReportKnowledgeGaps(gaps) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return '<p class="empty-state">Проблемные темы не найдены.</p>';
    }

    return `
        <div class="gap-list">
            ${gaps.map(gap => `
                <article class="gap-row">
                    <div class="gap-row__title">${escapeHtml(gap.AspectName || gap.aspectName || `Аспект #${gap.KnowledgeAspectId || gap.knowledgeAspectId}`)}</div>
                    <div class="gap-row__meta">
                        Тема: ${escapeHtml(gap.TopicName || gap.topicName || 'не указана')}<br>
                        Средний уровень пробела: ${formatValue(gap.AverageGapScore ?? gap.averageGapScore)}
                    </div>
                </article>
            `).join('')}
        </div>
    `;
}

function renderReportErrorTypes(errorTypes) {
    if (!Array.isArray(errorTypes) || errorTypes.length === 0) {
        return '<p class="empty-state">Основные типы ошибок не найдены.</p>';
    }

    return `
        <div class="gap-list">
            ${errorTypes.map(errorType => `
                <article class="gap-row">
                    <div class="gap-row__title">${escapeHtml(errorType.Name || errorType.name || errorType.Code || errorType.code || `Ошибка #${errorType.ErrorTypeId || errorType.errorTypeId}`)}</div>
                    <div class="gap-row__meta">
                        Количество: ${formatValue(errorType.Count ?? errorType.count)}<br>
                        Средняя severity: ${formatValue(errorType.AverageSeverity ?? errorType.averageSeverity)}
                    </div>
                </article>
            `).join('')}
        </div>
    `;
}

function renderReportRecommendations(recommendations) {
    if (!Array.isArray(recommendations) || recommendations.length === 0) {
        return `
            <div class="report-block">
                <h4>Рекомендации</h4>
                <p class="empty-state">Рекомендации не найдены.</p>
            </div>
        `;
    }

    const priorities = [
        ['High', 'Высокий приоритет'],
        ['Medium', 'Средний приоритет'],
        ['Low', 'Низкий приоритет']
    ];

    return `
        <div class="report-block">
            <h4>Рекомендации</h4>
            <div class="report-recommendation-groups">
                ${priorities.map(([priority, title]) => {
                    const items = recommendations.filter(item => String(item.Priority || item.priority || '').toLowerCase() === priority.toLowerCase());

                    if (items.length === 0) {
                        return '';
                    }

                    return `
                        <section class="report-priority-group">
                            <h5><span class="priority ${priorityClass(priority)}">${escapeHtml(title)}</span></h5>
                            <div class="recommendation-list">
                                ${items.map(item => `
                                    <article class="recommendation-item">
                                        <div class="recommendation-item__title">${escapeHtml(item.Title || item.title || 'Рекомендация')}</div>
                                        <div class="recommendation-item__meta">
                                            Тема: ${escapeHtml(item.TopicName || item.topicName || 'не указана')} |
                                            Уровень пробела: ${formatValue(item.GapScore ?? item.gapScore)} |
                                            Ошибки: ${formatValue(item.RelatedErrorCount ?? item.relatedErrorCount)}
                                        </div>
                                        <p class="recommendation-item__description">${escapeHtml(item.Description || item.description || '')}</p>
                                    </article>
                                `).join('')}
                            </div>
                        </section>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function toggleReportDetails(event) {
    const button = event.target.closest('.report-details-toggle');

    if (!button) {
        return;
    }

    const details = document.getElementById(button.dataset.detailsId);

    if (!details) {
        return;
    }

    const isOpen = details.classList.toggle('is-open');
    button.textContent = isOpen ? 'Скрыть детали' : 'Показать детали';
}

async function downloadReportFile(event) {
    const button = event.target.closest('.report-download-button');

    if (!button) {
        return;
    }

    const reportId = button.dataset.reportId;
    const format = button.dataset.exportFormat;
    const status = document.getElementById('reports-status');

    if (!reportId || !format) {
        setError(status, 'Не удалось определить отчёт для скачивания');
        return;
    }

    const extension = format === 'Pdf' ? 'pdf' : 'xlsx';
    const originalText = button.textContent;

    try {
        button.disabled = true;
        button.textContent = 'Скачивание...';
        status.textContent = '';
        status.classList.remove('error');

        const blob = await authorizedGetBlob(`/Reports/${encodeURIComponent(reportId)}/Export/${format}`);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `report-${reportId}.${extension}`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.URL.revokeObjectURL(url);

        status.textContent = `Файл report-${reportId}.${extension} подготовлен к скачиванию.`;
    } catch (error) {
        setError(status, error.message || 'Не удалось скачать отчёт');
    } finally {
        button.disabled = false;
        button.textContent = originalText;
    }
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

function scoreLevelClass(value) {
    const score = Number(value) || 0;

    if (score >= 75) return 'score--high';
    if (score >= 50) return 'score--medium';
    return 'score--low';
}

function clampPercent(value) {
    const numericValue = Number(value);

    if (Number.isNaN(numericValue)) {
        return 0;
    }

    return Math.max(0, Math.min(100, Math.round(numericValue)));
}

function translatePriority(priority) {
    const normalized = String(priority || '').toLowerCase();

    if (normalized === 'high') return 'Высокий';
    if (normalized === 'medium') return 'Средний';
    return 'Низкий';
}

function translateReportType(reportType) {
    const normalized = String(reportType || '').toLowerCase();

    if (normalized === 'student') return 'Студент';
    if (normalized === 'group') return 'Группа';
    return formatValue(reportType);
}

function translateRiskLevel(level) {
    const normalized = String(level || '').toLowerCase();

    if (normalized === 'high') return 'Высокий';
    if (normalized === 'medium') return 'Средний';
    if (normalized === 'low') return 'Низкий';
    return '-';
}

function formatDate(value) {
    if (!value) {
        return '-';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    return date.toLocaleString('ru-RU');
}

function formatJsonPreview(value) {
    const parsed = parseJson(value);

    if (!parsed) {
        return formatValue(value);
    }

    const parts = [];

    if (parsed.title) parts.push(parsed.title);
    if (parsed.reportType) parts.push(`тип: ${translateReportType(parsed.reportType)}`);
    if (parsed.userId) parts.push(`пользователь: ${parsed.userId}`);
    if (parsed.groupId) parts.push(`группа: ${parsed.groupId}`);
    if (parsed.recommendationsCount !== undefined) parts.push(`рекомендаций: ${parsed.recommendationsCount}`);

    return parts.length > 0 ? parts.join(', ') : formatJsonBlock(value);
}

function formatJsonBlock(value) {
    const parsed = parseJson(value);

    if (!parsed) {
        return formatValue(value);
    }

    return JSON.stringify(parsed, null, 2);
}

function parseJson(value) {
    if (!value) {
        return null;
    }

    try {
        return JSON.parse(value);
    } catch {
        return null;
    }
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
