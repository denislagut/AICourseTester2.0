document.addEventListener('DOMContentLoaded', () => {
    if (!restrictAccess()) return;

    if (sessionStorage.getItem('isTeacher') === 'true') {
        window.location.href = '/TeacherDashboard/TeacherDashboard.html';
        return;
    }

    const userFullName = sessionStorage.getItem('userFullName') || 'Студент';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Студент';

    document.getElementById('profile-tooltip__button-logout').addEventListener('click', event => {
        event.preventDefault();
        logout();
    });

    loadMyAnalytics();
});

async function logout() {
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
            sessionStorage.removeItem('isTeacher');
            window.location.href = '/LoginPage/LoginPage.html';
            return;
        }

        if (response.status === 401 && isTokenExpired(authtoken)) {
            await refreshToken();
        }
    } catch (error) {
        console.error('Ошибка при выходе:', error);
        alert('Произошла ошибка при выходе');
    }
}

async function authorizedGet(url, allowRetry = true) {
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');
    const response = await fetch(`${apiHost}${url}`, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.status === 401 && allowRetry && isTokenExpired(authtoken)) {
        const newToken = await refreshToken();
        if (newToken) {
            return authorizedGet(url, false);
        }
    }

    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Ошибка запроса ${response.status}: ${text.slice(0, 180)}`);
    }

    return response.json();
}

async function loadMyAnalytics() {
    const status = document.getElementById('student-analytics-status');

    try {
        status.textContent = 'Загрузка аналитики...';
        status.classList.remove('error');

        const analytics = await authorizedGet('/Analytics/Me');
        renderAnalytics(analytics);
        status.textContent = 'Данные обновлены.';
    } catch (error) {
        console.error('Не удалось загрузить аналитику:', error);
        status.textContent = 'Не удалось загрузить аналитику.';
        status.classList.add('error');
        document.getElementById('student-summary-grid').innerHTML = '';
        document.getElementById('student-gaps-content').innerHTML = renderEmptyState();
        document.getElementById('student-progress-content').innerHTML = renderEmptyState();
    }
}

function renderAnalytics(analytics) {
    const totalErrors = getValue(analytics, 'totalErrors', 'TotalErrors') ?? 0;
    const totalGaps = getValue(analytics, 'totalKnowledgeGaps', 'TotalKnowledgeGaps') ?? 0;
    const averageGapScore = getValue(analytics, 'averageGapScore', 'AverageGapScore') ?? 0;
    const highSeverityErrors = getValue(analytics, 'highSeverityErrorsCount', 'HighSeverityErrorsCount') ?? 0;
    const gaps = getValue(analytics, 'topKnowledgeGaps', 'TopKnowledgeGaps') || [];
    const progress = getValue(analytics, 'studentProgress', 'StudentProgress') ||
        getValue(analytics, 'learningProgress', 'LearningProgress');

    document.getElementById('student-summary-grid').innerHTML = `
        ${renderMetricCard('Количество ошибок', totalErrors)}
        ${renderMetricCard('Пробелы в знаниях', totalGaps)}
        ${renderMetricCard('Средний показатель проблемности', averageGapScore)}
        ${renderMetricCard('Серьёзные ошибки', highSeverityErrors)}
    `;

    const hasAnyData = totalErrors > 0 || totalGaps > 0 || gaps.length > 0;
    document.getElementById('student-gaps-content').innerHTML = hasAnyData
        ? renderKnowledgeGaps(gaps, progress)
        : renderEmptyState();

    document.getElementById('student-progress-content').innerHTML = renderLearningProgress(progress);
}

function renderMetricCard(label, value) {
    return `
        <article class="metric-card">
            <span class="metric-card__label">${escapeHtml(label)}</span>
            <strong class="metric-card__value">${escapeHtml(formatValue(value))}</strong>
        </article>
    `;
}

function renderKnowledgeGaps(gaps, progress) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return '<p class="empty-state">Пробелы в знаниях пока не найдены.</p>';
    }

    const progressByAspect = buildProgressByAspect(progress);

    return `
        <div class="gaps-grid">
            ${gaps.map(gap => {
                const aspectId = getValue(gap, 'knowledgeAspectId', 'KnowledgeAspectId');
                const progressItem = progressByAspect.get(Number(aspectId));
                const averageScore = getValue(gap, 'averageGapScore', 'AverageGapScore') ?? 0;
                const level = getValue(progressItem, 'level', 'Level') || getLevelByScore(averageScore);
                const trend = getValue(progressItem, 'trend', 'Trend');

                return `
                    <article class="gap-card">
                        <span class="level-badge ${levelClass(level)}">${escapeHtml(translateLevel(level))}</span>
                        <h3>${escapeHtml(getValue(gap, 'aspectName', 'AspectName') || `Аспект #${aspectId}`)}</h3>
                        <div class="gap-card__meta">
                            Тема: ${escapeHtml(getValue(gap, 'topicName', 'TopicName') || 'не указана')}<br>
                            Средний показатель: ${escapeHtml(formatValue(averageScore))}<br>
                            Максимальный показатель: ${escapeHtml(formatValue(getValue(gap, 'maxGapScore', 'MaxGapScore')))}<br>
                            Количество проявлений: ${escapeHtml(formatValue(getValue(gap, 'count', 'Count')))}
                            ${trend ? `<br>Тренд: ${escapeHtml(translateTrend(trend))}` : ''}
                        </div>
                    </article>
                `;
            }).join('')}
        </div>
    `;
}

function renderLearningProgress(progress) {
    const gapsProgress = getValue(progress, 'gapsProgress', 'GapsProgress') || [];
    const hasProgress = Boolean(progress) && (
        gapsProgress.length > 0 ||
        getValue(progress, 'previousAverageGapScore', 'PreviousAverageGapScore') != null ||
        Number(getValue(progress, 'currentAverageGapScore', 'CurrentAverageGapScore') || 0) > 0
    );

    if (!hasProgress) {
        return '<p class="empty-state">Недостаточно данных для оценки динамики обучения. Выполните несколько заданий, чтобы появилась статистика.</p>';
    }

    const current = getValue(progress, 'currentAverageGapScore', 'CurrentAverageGapScore');
    const previous = getValue(progress, 'previousAverageGapScore', 'PreviousAverageGapScore');
    const delta = getValue(progress, 'averageGapScoreDelta', 'AverageGapScoreDelta');
    const trendSummary = getValue(progress, 'trendSummary', 'TrendSummary');

    return `
        <div class="progress-summary">
            ${renderProgressMetric('Текущий показатель', current)}
            ${renderProgressMetric('Предыдущий показатель', previous)}
            ${renderProgressMetric('Изменение', formatSignedValue(delta))}
            ${renderProgressMetric('Улучшено', getValue(progress, 'improvedGapsCount', 'ImprovedGapsCount'))}
            ${renderProgressMetric('Ухудшилось', getValue(progress, 'worsenedGapsCount', 'WorsenedGapsCount'))}
            ${renderProgressMetric('Без изменений', getValue(progress, 'stableGapsCount', 'StableGapsCount'))}
            ${renderProgressMetric('Новые пробелы', getValue(progress, 'newGapsCount', 'NewGapsCount'))}
        </div>
        <p class="learning-conclusion">${escapeHtml(buildLearningConclusion(trendSummary, delta))}</p>
        ${renderProgressGaps(gapsProgress)}
    `;
}

function renderProgressMetric(label, value) {
    return `
        <div class="progress-summary__item">
            <span>${escapeHtml(label)}</span>
            <strong>${escapeHtml(formatValue(value))}</strong>
        </div>
    `;
}

function renderProgressGaps(gapsProgress) {
    if (!Array.isArray(gapsProgress) || gapsProgress.length === 0) {
        return '<p class="empty-state">Подробная динамика по аспектам пока отсутствует.</p>';
    }

    return `
        <div class="progress-gaps-grid">
            ${gapsProgress.map(item => {
                const trend = getValue(item, 'trend', 'Trend');
                const level = getValue(item, 'level', 'Level');

                return `
                    <article class="progress-card">
                        <span class="trend-badge ${trendClass(trend)}">${escapeHtml(translateTrend(trend))}</span>
                        <h3>${escapeHtml(getValue(item, 'aspectName', 'AspectName') || 'Аспект знаний')}</h3>
                        <div class="progress-card__meta">
                            Тема: ${escapeHtml(getValue(item, 'topic', 'Topic') || 'не указана')}<br>
                            Предыдущий показатель: ${escapeHtml(formatValue(getValue(item, 'previousGapScore', 'PreviousGapScore')))}<br>
                            Текущий показатель: ${escapeHtml(formatValue(getValue(item, 'currentGapScore', 'CurrentGapScore')))}<br>
                            Изменение: ${escapeHtml(formatSignedValue(getValue(item, 'gapScoreDelta', 'GapScoreDelta')))}<br>
                            Уровень: ${escapeHtml(translateLevel(level))}
                        </div>
                    </article>
                `;
            }).join('')}
        </div>
    `;
}

function buildProgressByAspect(progress) {
    const result = new Map();
    const gapsProgress = getValue(progress, 'gapsProgress', 'GapsProgress') || [];

    gapsProgress.forEach(item => {
        const aspectId = Number(getValue(item, 'knowledgeAspectId', 'KnowledgeAspectId'));
        if (aspectId > 0) {
            result.set(aspectId, item);
        }
    });

    return result;
}

function renderEmptyState() {
    return '<p class="empty-state">Данные аналитики пока отсутствуют. Выполните задания, чтобы появилась статистика.</p>';
}

function getValue(source, ...keys) {
    if (!source) return undefined;

    for (const key of keys) {
        if (source[key] !== undefined && source[key] !== null) {
            return source[key];
        }
    }

    return undefined;
}

function formatValue(value) {
    if (value === null || value === undefined || value === '') {
        return '-';
    }

    if (typeof value === 'number') {
        return Number.isInteger(value)
            ? value.toString()
            : value.toFixed(2).replace(/\.?0+$/, '');
    }

    return value;
}

function formatSignedValue(value) {
    const numericValue = Number(value);

    if (!Number.isFinite(numericValue)) {
        return '-';
    }

    if (numericValue > 0) {
        return `+${numericValue.toFixed(2).replace(/\.?0+$/, '')}`;
    }

    return numericValue.toFixed(2).replace(/\.?0+$/, '');
}

function getLevelByScore(score) {
    const numericScore = Number(score) || 0;

    if (numericScore >= 80) return 'High';
    if (numericScore >= 50) return 'Medium';
    return 'Low';
}

function translateLevel(level) {
    const normalized = String(level || '').toLowerCase();

    if (normalized === 'critical') return 'Критический';
    if (normalized === 'high') return 'Высокий';
    if (normalized === 'medium') return 'Средний';
    if (normalized === 'low') return 'Низкий';
    return '-';
}

function translateTrend(trend) {
    const normalized = String(trend || '').toLowerCase();

    if (normalized === 'improved') return 'Улучшение';
    if (normalized === 'worsened') return 'Ухудшение';
    if (normalized === 'new') return 'Новый пробел';
    if (normalized === 'stable') return 'Без изменений';
    return formatValue(trend);
}

function levelClass(level) {
    const normalized = String(level || '').toLowerCase();

    if (normalized === 'critical' || normalized === 'high') return 'level-badge--high';
    if (normalized === 'medium') return 'level-badge--medium';
    return 'level-badge--low';
}

function trendClass(trend) {
    const normalized = String(trend || '').toLowerCase();

    if (normalized === 'improved') return 'trend-badge--improved';
    if (normalized === 'worsened') return 'trend-badge--worsened';
    if (normalized === 'new') return 'trend-badge--new';
    return 'trend-badge--stable';
}

function buildLearningConclusion(trendSummary, delta) {
    const numericDelta = Number(delta);
    const formattedDelta = Number.isFinite(numericDelta)
        ? ` на ${Math.abs(numericDelta).toFixed(2).replace(/\.?0+$/, '')}`
        : '';
    const normalized = String(trendSummary || '').toLowerCase();

    if (normalized === 'improved') {
        return `Показатель проблемности снизился${formattedDelta}. Наблюдается улучшение.`;
    }

    if (normalized === 'worsened') {
        return `Показатель проблемности увеличился${formattedDelta}. Рекомендуется повторить проблемные темы.`;
    }

    if (normalized === 'stable') {
        return 'Существенных изменений не выявлено.';
    }

    return 'Недостаточно данных для уверенной оценки динамики.';
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
