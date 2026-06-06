let dashboardGroups = [];
const selectedStudentIds = { student: '', report: '', compare: '' };
let activeCompareDateInput = null;
let filterDictionaries = { errorTypes: [], knowledgeAspects: [] };
let analyticsFilters = { excludedErrorTypeIds: [], excludedKnowledgeAspectIds: [] };
const COMPARISON_TIME_OFFSET_HOURS = 6;

document.addEventListener('DOMContentLoaded', () => {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName') || 'Иванов И. И.';
    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = 'Преподаватель';

    document.getElementById('student-form').addEventListener('submit', loadStudentDashboard);
    document.getElementById('group-form').addEventListener('submit', loadGroupDashboard);
    document.getElementById('student-report-form').addEventListener('submit', generateStudentReport);
    document.getElementById('group-report-form').addEventListener('submit', generateGroupReport);
    document.getElementById('student-group-select').addEventListener('change', () => loadStudentsForGroup('student'));
    document.getElementById('student-select').addEventListener('change', event => selectedStudentIds.student = event.target.value);
    document.getElementById('report-student-group-select').addEventListener('change', () => loadStudentsForGroup('report'));
    document.getElementById('report-student-select').addEventListener('change', event => selectedStudentIds.report = event.target.value);
    document.getElementById('compare-student-group-select').addEventListener('change', () => loadStudentsForGroup('compare'));
    document.getElementById('compare-student-select').addEventListener('change', event => {
        selectedStudentIds.compare = event.target.value;
        loadCompareSnapshotDates();
    });
    document.getElementById('compare-group-select').addEventListener('change', loadCompareSnapshotDates);
    document.getElementById('compare-form').addEventListener('submit', compareAnalyticsPeriods);
    document.querySelectorAll('input[name="compare-mode"]').forEach(input => input.addEventListener('change', handleCompareModeChange));
    document.querySelectorAll('.compare-date-input').forEach(input => input.addEventListener('focus', () => activeCompareDateInput = input));
    document.getElementById('compare-date-chips').addEventListener('click', applySnapshotDateChip);
    document.getElementById('student-report-history-button').addEventListener('click', loadStudentReportHistory);
    document.getElementById('group-report-history-button').addEventListener('click', loadGroupReportHistory);
    document.getElementById('reports-content').addEventListener('click', toggleReportDetails);
    document.getElementById('reports-content').addEventListener('click', downloadReportFile);
    document.getElementById('apply-filters-button').addEventListener('click', applyAnalyticsFilters);
    document.getElementById('reset-filters-button').addEventListener('click', resetAnalyticsFilters);
    document.getElementById('profile-tooltip__button-logout').addEventListener('click', logout);

    loadSummary();
    loadDashboardGroups();
    loadFilterDictionaries();
});

async function authorizedGet(path) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const url = buildApiUrl(path);
    const response = await fetch(url, {
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
        throw new Error(await getResponseErrorMessage(response, url));
    }

    return readJsonResponse(response, url);
}

async function authorizedPost(path, body = null) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const url = buildApiUrl(path);
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        },
        body: body ? JSON.stringify(body) : null
    });

    if (response.status === 401) {
        const refreshedToken = await refreshToken();
        if (refreshedToken) {
            return authorizedPost(path, body);
        }
    }

    if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, url));
    }

    return readJsonResponse(response, url);
}

function buildApiUrl(path) {
    return `${apiHost}${path}`;
}

async function readJsonResponse(response, url) {
    const text = await response.text();

    if (!text) {
        return null;
    }

    try {
        return JSON.parse(text);
    } catch {
        throw new Error(`API вернул не JSON. URL: ${url}. Статус: ${response.status}. Ответ: ${text.slice(0, 200)}`);
    }
}

async function getResponseErrorMessage(response, url = '') {
    if (response.status === 404) {
        return url
            ? `Ошибка запроса 404. Пользователь или группа не найдены. URL: ${url}`
            : 'Пользователь или группа не найдены';
    }

    const text = await response.text();

    if (text) {
        return url
            ? `Ошибка запроса ${response.status}. URL: ${url}. Ответ: ${text.replace(/^"|"$/g, '').slice(0, 200)}`
            : text.replace(/^"|"$/g, '');
    }

    return url
        ? `Ошибка запроса ${response.status}. URL: ${url}`
        : `Ошибка запроса: ${response.status}`;
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

async function authorizedGetDetailed(path) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const url = buildApiUrl(path);
    const response = await fetch(url, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        }
    });

    if (response.status === 401) {
        const refreshedToken = await refreshToken();
        if (refreshedToken) {
            return authorizedGetDetailed(path);
        }
    }

    if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, url));
    }

    return readJsonResponse(response, url);
}

async function authorizedGetBlob(path) {
    let authtoken = Cookies.get('.AspNetCore.Identity.Application');

    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    if (isTokenExpired(authtoken)) {
        authtoken = await refreshToken();
    }

    const url = buildApiUrl(path);
    const response = await fetch(url, {
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
        throw new Error(await getResponseErrorMessage(response, url));
    }

    return response.blob();
}

async function loadDashboardGroups() {
    const statuses = [
        document.getElementById('student-status'),
        document.getElementById('reports-status'),
        document.getElementById('compare-status')
    ];

    try {
        dashboardGroups = await authorizedGet('/Users/Groups');
        populateGroupSelect('student-group-select', dashboardGroups);
        populateGroupSelect('report-student-group-select', dashboardGroups);
        populateGroupSelect('group-analytics-select', dashboardGroups);
        populateGroupSelect('group-report-select', dashboardGroups);
        populateGroupSelect('compare-student-group-select', dashboardGroups);
        populateGroupSelect('compare-group-select', dashboardGroups);
    } catch (error) {
        statuses.forEach(status => setError(status, `Не удалось загрузить список групп: ${error.message}`));
    }
}

function populateGroupSelect(selectId, groups) {
    const select = document.getElementById(selectId);
    select.innerHTML = '<option value="">Выберите группу</option>';

    if (!Array.isArray(groups) || groups.length === 0) {
        select.innerHTML = '<option value="">Группы не найдены</option>';
        select.disabled = true;
        return;
    }

    select.disabled = false;
    select.innerHTML += groups.map(group => `
        <option value="${escapeHtml(group.id)}">${escapeHtml(group.name || `Группа #${group.id}`)}</option>
    `).join('');
}

async function loadFilterDictionaries() {
    const status = document.getElementById('filters-status');

    try {
        const [errorTypes, knowledgeAspects] = await Promise.all([
            authorizedGet('/Analytics/ErrorTypes'),
            authorizedGet('/Analytics/KnowledgeAspects')
        ]);

        filterDictionaries = {
            errorTypes: Array.isArray(errorTypes) ? errorTypes : [],
            knowledgeAspects: Array.isArray(knowledgeAspects) ? knowledgeAspects : []
        };

        renderFilterLists();
        renderActiveFiltersSummary();
        status.textContent = '';
        status.classList.remove('error');
    } catch (error) {
        setError(status, `Не удалось загрузить фильтры: ${error.message}`);
        document.getElementById('error-type-filter-list').innerHTML = '<span class="empty-state">Типы ошибок не загружены.</span>';
        document.getElementById('knowledge-aspect-filter-list').innerHTML = '<span class="empty-state">Аспекты знаний не загружены.</span>';
    }
}

function renderFilterLists() {
    document.getElementById('error-type-filter-list').innerHTML = renderCheckboxList(
        filterDictionaries.errorTypes,
        'excluded-error-type',
        item => `${item.name || item.Name || 'Тип ошибки'}${item.code || item.Code ? ` (${item.code || item.Code})` : ''}`,
        'Типы ошибок не найдены.'
    );
    document.getElementById('knowledge-aspect-filter-list').innerHTML = renderCheckboxList(
        filterDictionaries.knowledgeAspects,
        'excluded-knowledge-aspect',
        item => {
            const topic = item.topic || item.Topic || 'тема не указана';
            const name = item.name || item.Name || 'Аспект знания';
            return `${topic}: ${name}`;
        },
        'Аспекты знаний не найдены.'
    );
}

function renderCheckboxList(items, name, labelFactory, emptyText) {
    if (!Array.isArray(items) || items.length === 0) {
        return `<span class="empty-state">${escapeHtml(emptyText)}</span>`;
    }

    return items.map(item => {
        const id = item.id ?? item.Id;
        const description = item.description || item.Description || '';

        return `
            <label class="filter-checkbox-item">
                <input type="checkbox" name="${escapeHtml(name)}" value="${escapeHtml(id)}">
                <span>
                    <strong>${escapeHtml(labelFactory(item))}</strong>
                    ${description ? `<small>${escapeHtml(description)}</small>` : ''}
                </span>
            </label>
        `;
    }).join('');
}

function applyAnalyticsFilters() {
    analyticsFilters = collectAnalyticsFilters();
    renderActiveFiltersSummary();
    const status = document.getElementById('filters-status');
    status.textContent = hasActiveFilters() ? 'Фильтр применён.' : 'Фильтры не выбраны.';
    status.classList.remove('error');
    refreshVisibleAnalyticsBlocks();
}

function resetAnalyticsFilters() {
    document.querySelectorAll('input[name="excluded-error-type"], input[name="excluded-knowledge-aspect"]')
        .forEach(input => input.checked = false);
    analyticsFilters = { excludedErrorTypeIds: [], excludedKnowledgeAspectIds: [] };
    renderActiveFiltersSummary();
    const status = document.getElementById('filters-status');
    status.textContent = 'Фильтр сброшен.';
    status.classList.remove('error');
    refreshVisibleAnalyticsBlocks();
}

function refreshVisibleAnalyticsBlocks() {
    const studentContent = document.getElementById('student-content');
    const groupContent = document.getElementById('group-content');

    if (getSelectedStudentId('student') && studentContent.innerHTML.trim()) {
        loadStudentDashboard();
    }

    if (getSelectedGroupId('group-analytics-select') && groupContent.innerHTML.trim()) {
        loadGroupDashboard();
    }
}

function collectAnalyticsFilters() {
    return {
        excludedErrorTypeIds: collectCheckedIds('excluded-error-type'),
        excludedKnowledgeAspectIds: collectCheckedIds('excluded-knowledge-aspect')
    };
}

function collectCheckedIds(name) {
    return Array.from(document.querySelectorAll(`input[name="${name}"]:checked`))
        .map(input => Number(input.value))
        .filter(id => Number.isInteger(id) && id > 0);
}

function hasActiveFilters() {
    return analyticsFilters.excludedErrorTypeIds.length > 0 ||
        analyticsFilters.excludedKnowledgeAspectIds.length > 0;
}

function renderActiveFiltersSummary() {
    const container = document.getElementById('active-filters-summary');

    if (!hasActiveFilters()) {
        container.textContent = 'Фильтры не выбраны.';
        return;
    }

    container.innerHTML = `
        <strong>Применены фильтры:</strong>
        <span>исключённые типы ошибок: ${escapeHtml(formatFilterNames(analyticsFilters.excludedErrorTypeIds, filterDictionaries.errorTypes, formatErrorTypeFilterName))}</span>
        <span>исключённые аспекты знаний: ${escapeHtml(formatFilterNames(analyticsFilters.excludedKnowledgeAspectIds, filterDictionaries.knowledgeAspects, formatKnowledgeAspectFilterName))}</span>
    `;
}

function formatFilterNames(ids, dictionary, formatter) {
    if (!Array.isArray(ids) || ids.length === 0) {
        return 'не выбраны';
    }

    return ids
        .map(id => {
            const item = dictionary.find(entry => Number(entry.id ?? entry.Id) === Number(id));
            return item ? formatter(item) : `#${id}`;
        })
        .join(', ');
}

function formatErrorTypeFilterName(item) {
    const code = item.code || item.Code;
    const name = item.name || item.Name || 'Тип ошибки';
    return code ? `${name} (${code})` : name;
}

function formatKnowledgeAspectFilterName(item) {
    const topic = item.topic || item.Topic;
    const name = item.name || item.Name || 'Аспект знания';
    return topic ? `${topic}: ${name}` : name;
}

function buildAnalyticsFilterQuery(prefix = '?') {
    analyticsFilters = collectAnalyticsFilters();
    renderActiveFiltersSummary();

    const params = new URLSearchParams();
    analyticsFilters.excludedErrorTypeIds.forEach(id => params.append('excludedErrorTypeIds', id));
    analyticsFilters.excludedKnowledgeAspectIds.forEach(id => params.append('excludedKnowledgeAspectIds', id));
    const query = params.toString();
    return query ? `${prefix}${query}` : '';
}

function buildReportFilterBody() {
    analyticsFilters = collectAnalyticsFilters();
    renderActiveFiltersSummary();

    return {
        excludedErrorTypeIds: analyticsFilters.excludedErrorTypeIds,
        excludedKnowledgeAspectIds: analyticsFilters.excludedKnowledgeAspectIds
    };
}

async function loadStudentsForGroup(scope) {
    const config = {
        student: {
            groupSelectId: 'student-group-select',
            studentSelectId: 'student-select',
            statusId: 'student-status'
        },
        report: {
            groupSelectId: 'report-student-group-select',
            studentSelectId: 'report-student-select',
            statusId: 'reports-status'
        },
        compare: {
            groupSelectId: 'compare-student-group-select',
            studentSelectId: 'compare-student-select',
            statusId: 'compare-status'
        }
    }[scope];

    const groupSelectId = config.groupSelectId;
    const studentSelectId = config.studentSelectId;
    const status = document.getElementById(config.statusId);
    const groupId = document.getElementById(groupSelectId).value;

    resetStudentSelect(studentSelectId, 'Выберите студента');
    selectedStudentIds[scope] = '';
    status.textContent = '';
    status.classList.remove('error');

    if (scope === 'compare') {
        clearCompareSnapshotDates('Выберите студента или группу');
        document.getElementById('compare-content').innerHTML = '';
    }

    if (!groupId) {
        resetStudentSelect(studentSelectId, 'Сначала выберите группу');
        return;
    }

    try {
        resetStudentSelect(studentSelectId, 'Загрузка студентов...');
        const students = await loadExistingGroupUsers(groupId);
        populateStudentSelect(studentSelectId, students);

        if (!Array.isArray(students) || students.length === 0) {
            status.textContent = 'В выбранной группе нет студентов';
        }
    } catch (error) {
        resetStudentSelect(studentSelectId, 'Выберите студента');
        setError(status, error.message);
    }
}

function resetStudentSelect(selectId, placeholder) {
    const select = document.getElementById(selectId);
    select.disabled = true;
    select.innerHTML = `<option value="">${escapeHtml(placeholder)}</option>`;
}

function populateStudentSelect(selectId, students) {
    const select = document.getElementById(selectId);
    select.innerHTML = '<option value="">Выберите студента</option>';

    if (!Array.isArray(students) || students.length === 0) {
        select.innerHTML = '<option value="">В выбранной группе нет студентов</option>';
        select.disabled = true;
        return;
    }

    select.disabled = false;
    select.innerHTML += students.map(student => `
        <option value="${escapeHtml(student.id)}">${escapeHtml(formatStudentOption(student))}</option>
    `).join('');
}

async function loadExistingGroupUsers(groupId) {
    try {
        return await authorizedGet(`/Users/Groups/${encodeURIComponent(groupId)}/`);
    } catch (error) {
        if (String(error.message).includes('404')) {
            return [];
        }

        throw error;
    }
}

function formatStudentOption(student) {
    const fullName = [student.secondName, student.name, student.patronymic]
        .filter(Boolean)
        .join(' ')
        .trim();
    const name = student.fullName || fullName || student.userName || student.email || student.id;
    const login = student.userName && student.userName !== name ? ` (${student.userName})` : '';
    return `${name}${login}`;
}

function getSelectedStudentId(scope) {
    const selectMap = {
        student: 'student-select',
        report: 'report-student-select',
        compare: 'compare-student-select'
    };
    const selectId = selectMap[scope] || 'student-select';
    const value = document.getElementById(selectId).value;
    selectedStudentIds[scope] = value;
    return value;
}

function getSelectedGroupId(selectId) {
    return document.getElementById(selectId).value;
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
    event?.preventDefault();

    const userId = getSelectedStudentId('student');
    const status = document.getElementById('student-status');
    const content = document.getElementById('student-content');

    if (!userId) {
        setError(status, 'Выберите студента');
        return;
    }

    setLoading(status, content);

    try {
        const filterQuery = buildAnalyticsFilterQuery();
        const [analytics, recommendations] = await Promise.all([
            authorizedGet(`/Analytics/Students/${encodeURIComponent(userId)}${filterQuery}`),
            authorizedGet(`/Recommendations/Students/${encodeURIComponent(userId)}${filterQuery}`)
        ]);

        renderStudentAnalytics(analytics, recommendations);
        status.textContent = '';
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function loadGroupDashboard(event) {
    event?.preventDefault();

    const groupId = getSelectedGroupId('group-analytics-select');
    const status = document.getElementById('group-status');
    const content = document.getElementById('group-content');

    if (!groupId) {
        setError(status, 'Выберите группу');
        return;
    }

    setLoading(status, content);

    try {
        const filterQuery = buildAnalyticsFilterQuery();
        const [analytics, recommendations] = await Promise.all([
            authorizedGet(`/Analytics/Groups/${encodeURIComponent(groupId)}${filterQuery}`),
            authorizedGet(`/Recommendations/Groups/${encodeURIComponent(groupId)}${filterQuery}`)
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

    const userId = getSelectedStudentId('report');
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!userId) {
        setError(status, 'Выберите студента');
        return;
    }

    setLoading(status, content);

    try {
        const report = await authorizedPost(
            `/Reports/Students/${encodeURIComponent(userId)}/Generate`,
            buildReportFilterBody());
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

    const groupId = getSelectedGroupId('group-report-select');
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!groupId) {
        setError(status, 'Выберите группу');
        return;
    }

    setLoading(status, content);

    try {
        const report = await authorizedPost(
            `/Reports/Groups/${encodeURIComponent(groupId)}/Generate`,
            buildReportFilterBody());
        status.textContent = 'Отчёт по группе успешно сформирован.';
        status.classList.remove('error');
        content.innerHTML = renderReports([report]);
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message);
    }
}

async function loadStudentReportHistory() {
    const userId = getSelectedStudentId('report');
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!userId) {
        setError(status, 'Выберите студента');
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
    const groupId = getSelectedGroupId('group-report-select');
    const status = document.getElementById('reports-status');
    const content = document.getElementById('reports-content');

    if (!groupId) {
        setError(status, 'Выберите группу');
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

function handleCompareModeChange() {
    const mode = getCompareMode();
    const studentControls = document.getElementById('compare-student-controls');
    const groupControls = document.getElementById('compare-group-controls');
    const content = document.getElementById('compare-content');
    const status = document.getElementById('compare-status');

    studentControls.classList.toggle('is-hidden', mode !== 'student');
    groupControls.classList.toggle('is-hidden', mode !== 'group');
    content.innerHTML = '';
    status.textContent = '';
    status.classList.remove('error');
    loadCompareSnapshotDates();
}

function getCompareMode() {
    return document.querySelector('input[name="compare-mode"]:checked')?.value || 'student';
}

async function loadCompareSnapshotDates() {
    const mode = getCompareMode();
    const status = document.getElementById('compare-status');

    status.textContent = '';
    status.classList.remove('error');

    const targetId = mode === 'student'
        ? getSelectedStudentId('compare')
        : getSelectedGroupId('compare-group-select');

    if (!targetId) {
        clearCompareSnapshotDates(mode === 'student' ? 'Выберите студента' : 'Выберите группу');
        return;
    }

    try {
        const path = mode === 'student'
            ? `/Analytics/Students/${encodeURIComponent(targetId)}/ActivityDates`
            : `/Analytics/Groups/${encodeURIComponent(targetId)}/ActivityDates`;
        const activityDates = await authorizedGetWithMessage(path);
        renderCompareDateChips(activityDates);
    } catch (error) {
        clearCompareSnapshotDates('Даты с данными не найдены');
        setError(status, error.message);
    }
}

function clearCompareSnapshotDates(message) {
    document.getElementById('compare-date-chips').innerHTML = `<span class="empty-state">${escapeHtml(message)}</span>`;
}

function renderCompareDateChips(activityDates) {
    const container = document.getElementById('compare-date-chips');

    if (!Array.isArray(activityDates) || activityDates.length === 0) {
        clearCompareSnapshotDates('Даты с данными не найдены');
        return;
    }

    const uniqueDates = new Map();

    activityDates.forEach(dateValue => {
        const dateInfo = formatActivityDateChip(dateValue);

        if (dateInfo && !uniqueDates.has(dateInfo.inputValue)) {
            uniqueDates.set(dateInfo.inputValue, dateInfo.displayValue);
        }
    });

    if (uniqueDates.size === 0) {
        clearCompareSnapshotDates('Даты с данными не найдены');
        return;
    }

    container.innerHTML = Array.from(uniqueDates.entries()).map(([inputValue, displayValue]) => `
        <button type="button" class="date-chip" data-date="${escapeHtml(inputValue)}">${escapeHtml(displayValue)}</button>
    `).join('');
}

function formatActivityDateChip(dateValue) {
    const normalizedDate = String(dateValue || '').slice(0, 10);
    const match = normalizedDate.match(/^(\d{4})-(\d{2})-(\d{2})$/);

    if (!match) {
        return null;
    }

    return {
        displayValue: `${match[3]}.${match[2]}.${match[1]}`,
        inputValue: normalizedDate
    };
}

function applySnapshotDateChip(event) {
    const chip = event.target.closest('.date-chip');

    if (!chip) {
        return;
    }

    const dateValue = chip.dataset.date;
    const dateInputs = Array.from(document.querySelectorAll('.compare-date-input'));
    const targetInput = activeCompareDateInput && dateInputs.includes(activeCompareDateInput)
        ? activeCompareDateInput
        : dateInputs.find(input => !input.value) || dateInputs[0];

    if (targetInput) {
        targetInput.value = dateValue;
        targetInput.focus();
        activeCompareDateInput = targetInput;
    }
}

async function compareAnalyticsPeriods(event) {
    event.preventDefault();

    const mode = getCompareMode();
    const status = document.getElementById('compare-status');
    const content = document.getElementById('compare-content');
    const targetId = mode === 'student'
        ? getSelectedStudentId('compare')
        : getSelectedGroupId('compare-group-select');

    if (!targetId) {
        setError(status, mode === 'student' ? 'Выберите студента' : 'Выберите группу');
        return;
    }

    status.textContent = 'Сравнение периодов...';
    status.classList.remove('error');
    content.innerHTML = '';

    try {
        const query = buildCompareQuery();
        const path = mode === 'student'
            ? `/Analytics/Students/${encodeURIComponent(targetId)}/Compare?${query}`
            : `/Analytics/Groups/${encodeURIComponent(targetId)}/Compare?${query}`;
        const comparison = await authorizedGetDetailed(path);

        content.innerHTML = renderCompareResult(comparison);
        status.textContent = '';
    } catch (error) {
        content.innerHTML = '';
        setError(status, error.message || 'Недостаточно данных для сравнения выбранных периодов');
    }
}

function buildCompareQuery() {
    const params = new URLSearchParams();
    const fields = [
        ['beforeFrom', 'compare-before-from'],
        ['beforeTo', 'compare-before-to'],
        ['afterFrom', 'compare-after-from'],
        ['afterTo', 'compare-after-to']
    ];

    fields.forEach(([name, id]) => {
        const value = document.getElementById(id).value;

        if (value) {
            params.set(name, value);
        }
    });
    analyticsFilters.excludedErrorTypeIds.forEach(id => params.append('excludedErrorTypeIds', id));
    analyticsFilters.excludedKnowledgeAspectIds.forEach(id => params.append('excludedKnowledgeAspectIds', id));

    return params.toString();
}

function renderCompareResult(comparison) {
    const before = comparison.before || comparison.Before || {};
    const after = comparison.after || comparison.After || {};
    const difference = comparison.difference || comparison.Difference || {};
    const metrics = [
        ['Ошибки', before.totalErrors ?? before.TotalErrors, after.totalErrors ?? after.TotalErrors, difference.totalErrors ?? difference.TotalErrors],
        ['Пробелы знаний', before.totalKnowledgeGaps ?? before.TotalKnowledgeGaps, after.totalKnowledgeGaps ?? after.TotalKnowledgeGaps, difference.totalKnowledgeGaps ?? difference.TotalKnowledgeGaps],
        ['Средний показатель проблемности', before.averageGapScore ?? before.AverageGapScore, after.averageGapScore ?? after.AverageGapScore, difference.averageGapScore ?? difference.AverageGapScore],
        ['Ошибки высокой серьёзности', before.highSeverityErrorsCount ?? before.HighSeverityErrorsCount, after.highSeverityErrorsCount ?? after.HighSeverityErrorsCount, difference.highSeverityErrorsCount ?? difference.HighSeverityErrorsCount]
    ];

    return `
        <div class="compare-result">
            <div class="compare-interpretation">
                <span>Интерпретация</span>
                <strong>${escapeHtml(comparison.interpretation || comparison.Interpretation || 'Существенных изменений не выявлено')}</strong>
            </div>
            <div class="compare-snapshot-dates">
                <span>До: ${escapeHtml(formatComparisonDateTime(before.createdAt || before.CreatedAt))}</span>
                <span>После: ${escapeHtml(formatComparisonDateTime(after.createdAt || after.CreatedAt))}</span>
            </div>
            <div class="compare-cards">
                ${metrics.map(([label, beforeValue, afterValue, diffValue]) => renderCompareMetric(label, beforeValue, afterValue, diffValue)).join('')}
            </div>
            <div class="compare-bars">
                <h3>Динамика среднего показателя проблемности</h3>
                ${renderCompareGapBar('До', before.averageGapScore ?? before.AverageGapScore)}
                ${renderCompareGapBar('После', after.averageGapScore ?? after.AverageGapScore)}
            </div>
        </div>
    `;
}

function renderCompareMetric(label, beforeValue, afterValue, diffValue) {
    const numericDiff = Number(diffValue) || 0;
    const trend = numericDiff < 0 ? 'улучшение' : numericDiff > 0 ? 'ухудшение' : 'без изменений';
    const trendClass = numericDiff < 0 ? 'compare-trend--good' : numericDiff > 0 ? 'compare-trend--bad' : 'compare-trend--neutral';

    return `
        <article class="compare-card">
            <h3>${escapeHtml(label)}</h3>
            <div class="compare-card__values">
                <span>Было: <strong>${formatValue(beforeValue)}</strong></span>
                <span>Стало: <strong>${formatValue(afterValue)}</strong></span>
                <span>Изменение: <strong>${escapeHtml(formatSignedNumber(diffValue))}</strong></span>
            </div>
            <span class="compare-trend ${trendClass}">${escapeHtml(trend)}</span>
        </article>
    `;
}

function renderCompareGapBar(label, value) {
    const percent = clampPercent(Number(value) || 0);

    return `
        <div class="compare-bar-row">
            <div class="compare-bar-row__top">
                <span>${escapeHtml(label)}</span>
                <strong>${formatValue(value)}%</strong>
            </div>
            <div class="score-indicator__track">
                <div class="score-indicator__bar ${scoreLevelClass(percent)}" style="width: ${percent}%"></div>
            </div>
        </div>
    `;
}

function formatSignedNumber(value) {
    const numericValue = Number(value);

    if (Number.isNaN(numericValue)) {
        return formatValue(value);
    }

    return numericValue > 0 ? `+${formatValue(value)}` : formatValue(value);
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
    const studentProgress = getStudentLearningProgress(analytics);

    content.innerHTML = `
        ${renderAnalyticsFilterNotice()}
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
        ${renderPanel('Динамика обучения', renderLearningProgress(studentProgress), 'panel--wide learning-progress-panel')}
    `;
}

function renderGroupAnalytics(analytics, recommendations) {
    const content = document.getElementById('group-content');
    const studentsStatistics = analytics.studentsStatistics || analytics.StudentsStatistics || [];
    const groupProgress = getGroupLearningProgress(analytics);

    content.innerHTML = `
        ${renderAnalyticsFilterNotice()}
        ${renderPanel('Статистика группы', renderDetails([
            ['Идентификатор группы', analytics.groupId],
            ['Название группы', analytics.groupName],
            ['Количество студентов', analytics.studentsCount],
            ['Количество ошибок', analytics.totalErrors],
            ['Количество пробелов', analytics.totalKnowledgeGaps],
            ['Средний показатель пробела', analytics.averageGapScore],
            ['Серьёзные ошибки', analytics.highSeverityErrorsCount]
        ]))}
        ${renderPanel('Статистика студентов группы', renderGroupStudentsStatistics(studentsStatistics), 'panel--wide')}
        ${renderPanel('Рекомендации группы', renderRecommendations(recommendations), 'panel--wide group-recommendations-panel')}
        ${renderPanel('Динамика обучения группы', renderLearningProgress(groupProgress), 'panel--wide learning-progress-panel')}
    `;
}

function renderAnalyticsFilterNotice() {
    if (!hasActiveFilters()) {
        return '';
    }

    return `
        <div class="active-filters-summary panel--wide">
            <strong>Аналитика рассчитана с фильтрами:</strong>
            <span>исключённые типы ошибок: ${escapeHtml(formatFilterNames(analyticsFilters.excludedErrorTypeIds, filterDictionaries.errorTypes, formatErrorTypeFilterName))}</span>
            <span>исключённые аспекты знаний: ${escapeHtml(formatFilterNames(analyticsFilters.excludedKnowledgeAspectIds, filterDictionaries.knowledgeAspects, formatKnowledgeAspectFilterName))}</span>
        </div>
    `;
}

function renderPanel(title, body, extraClass = '') {
    return `
        <section class="panel ${escapeHtml(extraClass)}">
            <h3>${escapeHtml(title)}</h3>
            ${body}
        </section>
    `;
}

function getStudentLearningProgress(analytics) {
    return analytics?.studentProgress ||
        analytics?.StudentProgress ||
        analytics?.learningProgress ||
        analytics?.LearningProgress ||
        null;
}

function getGroupLearningProgress(analytics) {
    return analytics?.groupProgress ||
        analytics?.GroupProgress ||
        analytics?.learningProgress ||
        analytics?.LearningProgress ||
        null;
}

function getAnyLearningProgress(analytics) {
    return getStudentLearningProgress(analytics) || getGroupLearningProgress(analytics);
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

function renderLearningProgress(progress, compact = false) {
    if (!progress) {
        return '<p class="empty-state">Недостаточно данных для оценки динамики обучения</p>';
    }

    const gapsProgress = progress.gapsProgress || progress.GapsProgress || [];
    const hasData = gapsProgress.length > 0 ||
        progress.previousAverageGapScore != null ||
        progress.PreviousAverageGapScore != null ||
        Number(progress.currentAverageGapScore ?? progress.CurrentAverageGapScore ?? 0) > 0;

    if (!hasData) {
        return '<p class="empty-state">Недостаточно данных для оценки динамики обучения</p>';
    }

    const current = progress.currentAverageGapScore ?? progress.CurrentAverageGapScore;
    const previous = progress.previousAverageGapScore ?? progress.PreviousAverageGapScore;
    const delta = progress.averageGapScoreDelta ?? progress.AverageGapScoreDelta;
    const trendSummary = progress.trendSummary || progress.TrendSummary;

    return `
        <div class="learning-progress">
            <div class="student-stat-card__metrics">
                ${renderStudentStatMetric('Текущий показатель', current)}
                ${renderStudentStatMetric('Предыдущий показатель', previous)}
                ${renderStudentStatMetric('Изменение', formatSignedValue(delta))}
                ${renderStudentStatMetric('Улучшено', progress.improvedGapsCount ?? progress.ImprovedGapsCount)}
                ${renderStudentStatMetric('Ухудшилось', progress.worsenedGapsCount ?? progress.WorsenedGapsCount)}
                ${renderStudentStatMetric('Без изменений', progress.stableGapsCount ?? progress.StableGapsCount)}
                ${renderStudentStatMetric('Новые пробелы', progress.newGapsCount ?? progress.NewGapsCount)}
            </div>
            <p class="learning-progress__summary">${escapeHtml(buildLearningProgressConclusion(trendSummary, delta))}</p>
            ${compact ? '' : renderLearningProgressGaps(gapsProgress)}
        </div>
    `;
}

function renderLearningProgressGaps(gapsProgress) {
    if (!Array.isArray(gapsProgress) || gapsProgress.length === 0) {
        return '<p class="empty-state">Недостаточно данных для оценки динамики обучения</p>';
    }

    return `
        <div class="gap-list">
            ${gapsProgress.map(item => {
                const previous = item.previousGapScore ?? item.PreviousGapScore;
                const current = item.currentGapScore ?? item.CurrentGapScore;
                const delta = item.gapScoreDelta ?? item.GapScoreDelta;
                const trend = item.trend || item.Trend;
                const level = item.level || item.Level;

                return `
                    <article class="gap-row">
                        <div class="gap-row__title">${escapeHtml(item.aspectName || item.AspectName || `Аспект #${item.knowledgeAspectId || item.KnowledgeAspectId}`)}</div>
                        <div class="gap-row__meta">
                            Тема: ${escapeHtml(item.topic || item.Topic || 'не указана')}<br>
                            Предыдущий показатель: ${formatValue(previous)} |
                            Текущий показатель: ${formatValue(current)} |
                            Изменение: ${formatSignedValue(delta)}<br>
                            Тренд: ${escapeHtml(translateTrend(trend))} |
                            Уровень: ${escapeHtml(translateRiskLevel(level))}
                        </div>
                    </article>
                `;
            }).join('')}
        </div>
    `;
}

function renderGroupStudentsStatistics(students) {
    if (!Array.isArray(students) || students.length === 0) {
        return '<p class="empty-state">Статистика студентов группы пока не найдена.</p>';
    }

    return `
        <div class="student-stat-list">
            ${students.map(student => {
                const gaps = student.topKnowledgeGaps || student.TopKnowledgeGaps || [];
                const errors = student.topErrorTypes || student.TopErrorTypes || [];
                const learningProgress = student.learningProgress || student.LearningProgress || null;

                return `
                    <article class="student-stat-card">
                        <div class="student-stat-card__header">
                            <div>
                                <h4>${escapeHtml(getStudentStatisticsName(student))}</h4>
                                <span>${escapeHtml(student.userName || student.UserName || student.userId || student.UserId || '')}</span>
                            </div>
                        </div>
                        <div class="student-stat-card__metrics">
                            ${renderStudentStatMetric('Ошибки', student.totalErrors ?? student.TotalErrors)}
                            ${renderStudentStatMetric('Пробелы знаний', student.totalKnowledgeGaps ?? student.TotalKnowledgeGaps)}
                            ${renderStudentStatMetric('Средний показатель', student.averageGapScore ?? student.AverageGapScore)}
                            ${renderStudentStatMetric('Серьёзные ошибки', student.highSeverityErrorsCount ?? student.HighSeverityErrorsCount)}
                            ${renderStudentStatMetric('Изменение', formatSignedValue(student.averageGapScoreDelta ?? student.AverageGapScoreDelta))}
                        </div>
                        <details class="student-stat-details">
                            <summary>Подробнее</summary>
                            <div class="student-stat-details__content">
                                <section>
                                    <h5>Динамика обучения</h5>
                                    ${renderLearningProgress(learningProgress, true)}
                                </section>
                                <section>
                                    <h5>Проблемные темы</h5>
                                    ${renderStudentTopKnowledgeGaps(gaps)}
                                </section>
                                <section>
                                    <h5>Типы ошибок</h5>
                                    ${renderStudentTopErrorTypes(errors)}
                                </section>
                            </div>
                        </details>
                    </article>
                `;
            }).join('')}
        </div>
    `;
}

function renderStudentStatMetric(label, value) {
    return `
        <div>
            <span>${escapeHtml(label)}</span>
            <strong>${escapeHtml(formatValue(value))}</strong>
        </div>
    `;
}

function getStudentStatisticsName(student) {
    return student.fullName || student.FullName || student.userName || student.UserName || student.userId || student.UserId || 'Студент';
}

function renderStudentTopKnowledgeGaps(gaps) {
    if (!Array.isArray(gaps) || gaps.length === 0) {
        return '<p class="empty-state">Проблемные темы не найдены.</p>';
    }

    return `
        <div class="student-stat-mini-list">
            ${gaps.map(gap => `
                <div>
                    <strong>${escapeHtml(gap.aspectName || gap.AspectName || `Аспект #${gap.knowledgeAspectId || gap.KnowledgeAspectId}`)}</strong>
                    <span>
                        Тема: ${escapeHtml(gap.topicName || gap.TopicName || 'не указана')} |
                        Средний показатель: ${formatValue(gap.averageGapScore ?? gap.AverageGapScore)}
                    </span>
                </div>
            `).join('')}
        </div>
    `;
}

function renderStudentTopErrorTypes(errors) {
    if (!Array.isArray(errors) || errors.length === 0) {
        return '<p class="empty-state">Типы ошибок не найдены.</p>';
    }

    return `
        <div class="student-stat-mini-list">
            ${errors.map(error => `
                <div>
                    <strong>${escapeHtml(error.name || error.Name || error.code || error.Code || `Ошибка #${error.errorTypeId || error.ErrorTypeId}`)}</strong>
                    <span>
                        Количество: ${formatValue(error.count ?? error.Count)} |
                        Средняя серьёзность: ${formatValue(error.averageSeverity ?? error.AverageSeverity)}
                    </span>
                </div>
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
                            ${renderReportFilters(summary)}
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

function renderReportFilters(summary) {
    const filters = summary?.filters || summary?.Filters;

    if (!filters) {
        return '';
    }

    const excludedErrorTypeIds = filters.excludedErrorTypeIds || filters.ExcludedErrorTypeIds || [];
    const excludedKnowledgeAspectIds = filters.excludedKnowledgeAspectIds || filters.ExcludedKnowledgeAspectIds || [];

    if ((!Array.isArray(excludedErrorTypeIds) || excludedErrorTypeIds.length === 0) &&
        (!Array.isArray(excludedKnowledgeAspectIds) || excludedKnowledgeAspectIds.length === 0)) {
        return '';
    }

    return `
        <div class="report-block report-filters-block">
            <h4>Применены фильтры</h4>
            <div class="details-list">
                <div>
                    <span>Исключённые типы ошибок</span>
                    <span>${escapeHtml(formatFilterNames(excludedErrorTypeIds, filterDictionaries.errorTypes, formatErrorTypeFilterName))}</span>
                </div>
                <div>
                    <span>Исключённые аспекты знаний</span>
                    <span>${escapeHtml(formatFilterNames(excludedKnowledgeAspectIds, filterDictionaries.knowledgeAspects, formatKnowledgeAspectFilterName))}</span>
                </div>
            </div>
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
    const studentsStatistics = analytics.StudentsStatistics || analytics.studentsStatistics || [];
    const learningProgress = getAnyLearningProgress(analytics);

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
            <h4>${studentsStatistics.length > 0 ? 'Динамика обучения группы' : 'Динамика обучения'}</h4>
            ${renderLearningProgress(learningProgress)}
        </div>
        ${renderReportGroupStudentsStatistics(studentsStatistics)}
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

function renderReportGroupStudentsStatistics(students) {
    if (!Array.isArray(students) || students.length === 0) {
        return '';
    }

    return `
        <div class="report-block">
            <h4>Статистика студентов группы</h4>
            ${renderGroupStudentsStatistics(students)}
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

function formatSignedValue(value) {
    if (value === null || value === undefined || value === '') {
        return '-';
    }

    const numericValue = Number(value);
    if (Number.isNaN(numericValue)) {
        return String(value);
    }

    if (numericValue > 0) {
        return `+${numericValue.toFixed(2).replace(/\.?0+$/, '')}`;
    }

    return numericValue.toFixed(2).replace(/\.?0+$/, '');
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

function buildLearningProgressConclusion(trendSummary, delta) {
    const numericDelta = Number(delta);
    const formattedDelta = Number.isNaN(numericDelta)
        ? ''
        : ` на ${Math.abs(numericDelta).toFixed(2).replace(/\.?0+$/, '')}`;
    const normalized = String(trendSummary || '').toLowerCase();

    if (normalized === 'improved') {
        return `Показатель проблемности снизился${formattedDelta} — наблюдается улучшение`;
    }

    if (normalized === 'worsened') {
        return `Показатель проблемности увеличился${formattedDelta} — требуется дополнительная работа`;
    }

    if (normalized === 'stable') {
        return 'Существенных изменений не выявлено';
    }

    return 'Недостаточно данных для оценки динамики';
}

function formatDate(value) {
    if (!value) {
        return '-';
    }

    const normalizedValue = typeof value === 'string'
        && /^\d{4}-\d{2}-\d{2}T/.test(value)
        && !/(Z|[+-]\d{2}:\d{2})$/i.test(value)
        ? `${value}Z`
        : value;
    const date = new Date(normalizedValue);

    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    return date.toLocaleString('ru-RU', {
        timeZone: 'Europe/Moscow',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function formatComparisonDateTime(value) {
    if (!value) {
        return '-';
    }

    const normalizedValue = normalizeUtcDateValue(value);
    const date = new Date(normalizedValue);

    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    const shiftedDate = addComparisonOffset(date);
    const dateInfo = formatComparisonDateParts(shiftedDate);
    const hours = String(shiftedDate.getUTCHours()).padStart(2, '0');
    const minutes = String(shiftedDate.getUTCMinutes()).padStart(2, '0');

    return `${dateInfo.displayValue}, ${hours}:${minutes}`;
}

function getComparisonDateInfo(value) {
    if (!value) {
        return null;
    }

    const normalizedValue = normalizeUtcDateValue(value);
    const date = new Date(normalizedValue);

    if (Number.isNaN(date.getTime())) {
        return null;
    }

    return formatComparisonDateParts(addComparisonOffset(date));
}

function normalizeUtcDateValue(value) {
    return typeof value === 'string'
        && /^\d{4}-\d{2}-\d{2}T/.test(value)
        && !/(Z|[+-]\d{2}:\d{2})$/i.test(value)
        ? `${value}Z`
        : value;
}

function addComparisonOffset(date) {
    return new Date(date.getTime() + COMPARISON_TIME_OFFSET_HOURS * 60 * 60 * 1000);
}

function formatComparisonDateParts(date) {
    const day = String(date.getUTCDate()).padStart(2, '0');
    const month = String(date.getUTCMonth() + 1).padStart(2, '0');
    const year = String(date.getUTCFullYear());

    return {
        displayValue: `${day}.${month}.${year}`,
        inputValue: `${year}-${month}-${day}`
    };
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
