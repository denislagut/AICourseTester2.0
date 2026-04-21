document.addEventListener('DOMContentLoaded', async function() {
    if (!restrictAccess()) return;

    const userFullName = sessionStorage.getItem('userFullName');
    const isTeacher = sessionStorage.getItem('isTeacher') === 'true' || false;

    if (!userFullName || sessionStorage.getItem('isTeacher') === null) {
        alert('Пожалуйста, войдите в систему');
        window.location.href = "/LoginPage/LoginPage.html";
        return;
    }

    document.querySelector('.profile-tooltip_username').textContent = userFullName;
    document.querySelector('.profile-tooltip_role').textContent = isTeacher ? 'Преподаватель' : 'Студент';
    
    const profileLink = document.querySelector('.profile-tooltip_to-profile-href');
    if (profileLink) {
        profileLink.href = isTeacher ? '/ProfileTeacherPage/ProfileTeacherPage.html' : '/ProfileStudentPage/ProfileStudentPage.html';
    }

    const adminLink = document.querySelector('.button_settings');
    if (adminLink && !isTeacher) {
        adminLink.style.display = 'none';
    }

    try {
        const urlParams = new URLSearchParams(window.location.search);
        const taskId = urlParams.get('taskId');
        const userId = urlParams.get('userId');
        const taskType = urlParams.get('taskType');
        const isViewMode = urlParams.get('view') === 'true';
        const isTrainingMode = taskType === 'train';

        console.log('Параметры URL:', { taskId, userId, taskType, isViewMode, isTrainingMode });

        if (isTrainingMode) {
            document.getElementById('training-mode').style.display = 'block';
            document.getElementById('tasksettings').style.display = 'block';
            document.getElementById('apply-settings').style.display = 'block';
        }

        const taskData = await fetchTaskData(taskId, userId, isViewMode, isTrainingMode);
        if (!taskData || !taskData.problem || taskData.problem.length === 0) {
            alert('Данные задачи не найдены');
            window.location.href = userId ? "/ProfileTeacherPage/ProfileTeacherPage.html" : "/ProfileStudentPage/ProfileStudentPage.html";
            return;
        }

        console.log('Полученные данные задачи:', taskData);

        if (isViewMode && taskData.originalData && taskData.originalData.user && taskData.originalData.user.name) {
            const studentInfo = document.getElementById('student-info');
            studentInfo.textContent = `Студент: ${taskData.originalData.user.name} ${taskData.originalData.user.secondName} ${taskData.originalData.user.patronymic}`;
        }

        displayHeuristicName(taskData.settings ? taskData.settings.heuristic : 1);

        window.taskData = taskData;
        window.openOrder = [];
        renderTree(taskData.problem, isViewMode);
        setupEventListeners(taskData, isViewMode, isTrainingMode);

        if (isViewMode || taskData.isSolved) {
            document.querySelectorAll('.heuristic-input').forEach(input => input.disabled = true);
            const submitButton = document.getElementById('submit-solution');
            if (isViewMode && submitButton) submitButton.style.display = 'none';
            else if (taskData.isSolved && submitButton) submitButton.style.display = 'block';
            const solutionMessage = document.getElementById('solution-message');
            if (solutionMessage) solutionMessage.style.display = 'block';
            if (taskData.userSolution && taskData.userSolution.length > 0) {
                displaySolutionFeedback(taskData);
                taskData.userSolution.forEach(({ id, h, f, g }) => {
                    const inputs = document.querySelectorAll(`.heuristic-input[data-node-id="${id}"]`);
                    inputs.forEach(input => {
                        if (input.dataset.param === 'h') input.value = h || '';
                        if (input.dataset.param === 'f') input.value = f || '';
                        if (input.dataset.param === 'g') input.value = g || '';
                    });
                });
            }
        }
    } catch (error) {
        console.error('Ошибка загрузки задачи:', error);
        alert('Не удалось загрузить задачу: ' + error.message);
        window.location.href = userId ? "/ProfileTeacherPage/ProfileTeacherPage.html" : "/ProfileStudentPage/ProfileStudentPage.html";
    }
});

function displayHeuristicName(heuristic) {
    const heuristicNameElement = document.getElementById('heuristic-name');
    if (heuristicNameElement) {
        heuristicNameElement.textContent = heuristic === 2 ? 'Эвристика: Манхэттенское расстояние' : 'Эвристика: Расстояние Хемминга';
    }
}

async function fetchTaskData(taskId, userId, isViewMode, isTrainingMode, settings = null) {
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');
    if (!authtoken) {
        throw new Error('Токен авторизации отсутствует');
    }

    let url;
    if (isTrainingMode) {
        url = `${apiHost}/A/FifteenPuzzle/Train`;
        if (settings) {
            const params = new URLSearchParams({
                dimensions: settings.dimensions,
                iters: settings.iters,
                heuristic: settings.heuristic
            });
            url += `?${params.toString()}`;
        }
    } else if (isViewMode) {
        url = `${apiHost}/A/FifteenPuzzle/Users/${userId}`;
    } else {
        url = `${apiHost}/A/FifteenPuzzle/Test`;
    }

    console.log('URL запроса:', url);

    try {
        const response = await fetch(url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Ошибка HTTP: ${response.status} ${errorText}`);
        }
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        const data = await response.json();
        console.log('Ответ API:', data);

        let formattedData = data;
        if (isTrainingMode) {
            formattedData = {
                problem: data,
                solution: [],
                userSolution: [],
                isSolved: false,
                settings: settings || { heuristic: 1 }
            };
        } else if (isViewMode && data.task) {
            formattedData = {
                problem: data.task.problem,
                solution: data.task.solution || [],
                userSolution: data.task.userSolution && data.task.userSolution.heuristicValues 
                    ? data.task.userSolution.heuristicValues 
                    : data.task.userSolution || [],
                isSolved: data.task.isSolved,
                date: data.task.date,
                originalData: data
            };
        } else if (!data.problem || !Array.isArray(data.problem) || data.problem.length === 0) {
            throw new Error('Данные задачи отсутствуют или имеют неверный формат');
        }

        const isValid = formattedData.problem.every(node => 
            typeof node === 'object' && 
            node !== null &&
            'id' in node &&
            'depth' in node &&
            'state' in node &&
            'subNodesIds' in node &&
            Array.isArray(node.state) &&
            Array.isArray(node.subNodesIds)
        );

        if (!isValid) {
            throw new Error('Некорректная структура данных задачи');
        }

        return formattedData;
    } catch (error) {
        console.error('Ошибка в fetchTaskData:', error);
        throw error;
    }
}

function renderTree(nodes, isViewMode) {
    const treeContainer = document.getElementById('tree-container');
    if (!treeContainer) {
        console.error('Контейнер tree-container не найден');
        return;
    }
    treeContainer.innerHTML = '';
    treeContainer.style.overflowX = 'auto';
    treeContainer.style.whiteSpace = 'nowrap';

    window.lines = [];

    const maxDepth = Math.max(...nodes.map(node => node.depth));
    console.log('Максимальная глубина дерева:', maxDepth);

    const levels = Array.from({ length: maxDepth + 1 }, (_, i) => {
        const levelContainer = document.createElement('div');
        levelContainer.className = `tree-level level-${i + 1}`;
        levelContainer.style.display = 'inline-flex';
        levelContainer.style.verticalAlign = 'top';
        if (i > 0) levelContainer.style.display = 'none';
        levelContainer.id = `level-${i + 1}`;
        treeContainer.appendChild(levelContainer);
        return levelContainer;
    });

    let canvas = document.getElementById('tree-canvas');
    if (!canvas) {
        canvas = document.createElement('canvas');
        canvas.id = 'tree-canvas';
        canvas.style.position = 'absolute';
        canvas.style.top = '0';
        canvas.style.left = '0';
        canvas.style.zIndex = '0';
        canvas.style.pointerEvents = 'none';
        treeContainer.appendChild(canvas);
    }

    function buildNode(node, level) {
        const nodeElement = createNodeElement(node, level, isViewMode);
        nodeElement.style.marginLeft = '20px';
        levels[level - 1].appendChild(nodeElement);

        if (node.subNodesIds && node.subNodesIds.length > 0) {
            const childrenContainer = document.createElement('div');
            childrenContainer.className = 'children-container';
            childrenContainer.id = `children-${node.id}`;
            childrenContainer.style.display = 'none';
            levels[level].appendChild(childrenContainer);

            node.subNodesIds.forEach(childId => {
                const childNode = nodes.find(n => n.id === childId);
                if (childNode) {
                    const childElement = buildNode(childNode, level + 1);
                    childrenContainer.appendChild(childElement);
                    window.lines.push({
                        start: nodeElement,
                        end: childElement,
                        containerId: `children-${node.id}`
                    });
                } else {
                    console.warn(`Узел с ID ${childId} не найден в данных`);
                }
            });

            nodeElement.addEventListener('click', (e) => {
                if (!e.target.classList.contains('heuristic-input')) {
                    console.log(`Клик на узел с ID ${node.id}, depth: ${level}`);
                    toggleChildren(node.id, level);
                }
            });
        }
        return nodeElement;
    }

    const rootNode = nodes.find(node => node.depth === 0);
    if (rootNode) {
        buildNode(rootNode, 1);
        console.log('Дерево построено, корневой узел:', rootNode.id);
    } else {
        console.error('Корневой узел не найден');
        alert('Не удалось построить дерево: корневой узел отсутствует');
    }

    canvas.width = treeContainer.scrollWidth;
    canvas.height = treeContainer.scrollHeight;
    canvas.style.width = `${treeContainer.scrollWidth}px`;
    canvas.style.height = `${treeContainer.scrollHeight}px`;
    drawLinesOnCanvas();
}

function createNodeElement(node, level, isViewMode) {
    const nodeElement = document.createElement('div');
    nodeElement.className = `tree-node level-${level}`;
    nodeElement.dataset.nodeId = node.id;

    const board = document.createElement('div');
    board.className = 'puzzle-board';
    const dimensions = node.state.length;
    board.style.gridTemplateColumns = `repeat(${dimensions}, 50px)`;
    
    node.state.flat().forEach(value => {
        const tile = document.createElement('div');
        tile.className = `puzzle-tile ${value === 0 ? 'empty' : ''}`;
        tile.textContent = value === 0 ? '' : value;
        board.appendChild(tile);
    });

    const inputsContainer = document.createElement('div');
    inputsContainer.className = 'heuristic-inputs';
    ['h', 'f', 'g'].forEach(param => {
        const inputGroup = document.createElement('div');
        inputGroup.className = 'input-group';
        inputGroup.innerHTML = `
            <label>${param}:</label>
            <input type="number" class="heuristic-input" data-param="${param}" data-node-id="${node.id}" min="0" ${isViewMode ? 'disabled' : ''}>
        `;
        inputsContainer.appendChild(inputGroup);
    });

    nodeElement.appendChild(board);
    nodeElement.appendChild(inputsContainer);

    if (!isViewMode) {
        inputsContainer.querySelectorAll('.heuristic-input').forEach(input => {
            input.addEventListener('input', function(e) {
                this.value = this.value.replace(/[^0-9]/g, '');
                if (this.value === '' || parseInt(this.value) < 0) {
                    this.value = '0';
                }
            });
            input.addEventListener('click', function(e) {
                e.stopPropagation();
            });
        });
    }

    return nodeElement;
}

function toggleChildren(nodeId, level, isRestoring = false) {
    const childrenContainer = document.getElementById(`children-${nodeId}`);
    const levelContainer = document.getElementById(`level-${level + 1}`);
    console.log(`Попытка переключения узла ${nodeId}, level ${level + 1}, isRestoring: ${isRestoring}`);
    if (childrenContainer && levelContainer) {
        if (childrenContainer.style.display === 'none') {
            levelContainer.style.display = 'inline-flex';
            childrenContainer.style.display = 'inline-flex';
            if (!isRestoring && !window.openOrder.includes(nodeId)) {
                window.openOrder.push(nodeId);
                console.log('Порядок раскрытия:', window.openOrder);
            }
        } else {
            childrenContainer.style.display = 'none';
            collapseChildren(nodeId);
            const visibleContainers = levelContainer.querySelectorAll('.children-container');
            const anyVisible = Array.from(visibleContainers).some(container => container.style.display === 'inline-flex');
            if (!anyVisible) {
                levelContainer.style.display = 'none';
            }
            window.openOrder = window.openOrder.filter(id => id !== nodeId);
            console.log('Порядок раскрытия после сворачивания:', window.openOrder);
        }
        drawLinesOnCanvas();
    } else {
        console.warn(`Контейнер для узла ${nodeId} или уровня ${level + 1} не найден`);
    }
}

function collapseChildren(nodeId) {
    const childrenContainer = document.getElementById(`children-${nodeId}`);
    if (childrenContainer) {
        const childNodes = childrenContainer.querySelectorAll('.tree-node');
        childNodes.forEach(node => {
            const childId = parseInt(node.dataset.nodeId);
            const childContainer = document.getElementById(`children-${childId}`);
            if (childContainer) {
                childContainer.style.display = 'none';
                window.openOrder = window.openOrder.filter(id => id !== childId);
                collapseChildren(childId);
            }
        });
        drawLinesOnCanvas();
    }
}

function setupEventListeners(taskData, isViewMode, isTrainingMode) {
    const submitButton = document.getElementById('submit-solution');
    const applySettingsButton = document.getElementById('apply-settings');
    const logoutButton = document.getElementById('profile-tooltip__button-logout');
    const treeContainer = document.getElementById('tree-container');

    if (submitButton) {
        const newSubmitButton = submitButton.cloneNode(true);
        submitButton.parentNode.replaceChild(newSubmitButton, submitButton);
        if (!isViewMode) {
            newSubmitButton.addEventListener('click', async () => {
                try {
                    const userSolution = collectSolution();
                    const validation = validateSolution(userSolution, taskData.problem);
                    if (!validation.isValid) {
                        alert(validation.message);
                        return;
                    }
                    const response = await submitSolution(userSolution, isTrainingMode, taskData);
                    displaySolutionFeedback({ ...taskData, userSolution, solution: response });
                } catch (error) {
                    const messageElement = document.getElementById('solution-message');
                    messageElement.style.display = 'block';
                    messageElement.classList.remove('success');
                    messageElement.classList.add('error');
                    messageElement.innerHTML = `Ошибка: ${error.message}`;
                }
            });
        }
    }

    if (applySettingsButton && isTrainingMode) {
        const newApplySettingsButton = applySettingsButton.cloneNode(true);
        applySettingsButton.parentNode.replaceChild(newApplySettingsButton, applySettingsButton);
        newApplySettingsButton.addEventListener('click', async () => {
            const dimensions = parseInt(document.getElementById('dimensions-input').value);
            const iters = parseInt(document.getElementById('iterations-input').value);
            const heuristic = parseInt(document.getElementById('heuristic-input').value);

            if (!dimensions || !iters || !heuristic || dimensions < 2 || dimensions > 4 || iters < 1 || iters > 10) {
                alert('Пожалуйста, заполните все поля корректными значениями');
                return;
            }

            try {
                const settings = { dimensions, iters, heuristic };
                const newTaskData = await fetchTaskData(null, null, false, true, settings);
                window.taskData = newTaskData;
                window.openOrder = [];
                renderTree(newTaskData.problem, isViewMode);
                setupEventListeners(newTaskData, isViewMode, isTrainingMode);
                document.getElementById('solution-message').style.display = 'none';
                displayHeuristicName(heuristic);
            } catch (error) {
                alert(`Не удалось применить настройки: ${error.message}`);
            }
        });
    }

    logoutButton.addEventListener('click', e => {
        e.preventDefault();
        Logout();
    });

    treeContainer.addEventListener('scroll', drawLinesOnCanvas);
    window.addEventListener('resize', drawLinesOnCanvas);
}

function validateSolution(userSolution, nodes) {
    const openNodeIds = window.openOrder;
    const inputs = document.querySelectorAll('.heuristic-input');
    const relevantInputs = Array.from(inputs).filter(input => openNodeIds.includes(parseInt(input.dataset.nodeId)));
    const emptyInputs = relevantInputs.filter(input => !input.value || isNaN(parseInt(input.value)));
    if (emptyInputs.length > 0) {
        return { isValid: false, message: 'Заполните все поля в раскрытых узлах значениями (только натуральные числа).' };
    }

    const maxDepth = Math.max(...nodes.map(node => node.depth));
    const maxOpenedDepth = Math.max(...openNodeIds.map(nodeId => nodes.find(n => n.id === nodeId)?.depth || 0)) + 1;
    if (maxOpenedDepth < maxDepth) {
        return { isValid: false, message: `Раскройте дерево до максимальной глубины (${maxDepth}). Текущая глубина: ${maxOpenedDepth}.` };
    }

    return { isValid: true, message: '' };
}

function collectSolution() {
    const solution = [];
    const visibleNodes = document.querySelectorAll('.tree-node');
    
    visibleNodes.forEach(node => {
        const nodeId = parseInt(node.dataset.nodeId);
        const levelContainer = node.closest('.tree-level');
        if (levelContainer && getComputedStyle(levelContainer).display !== 'none') {
            const nodeSolution = {
                id: nodeId,
                g: 0,
                h: 0,
                f: 0
            };
            solution.push(nodeSolution);
        }
    });

    document.querySelectorAll('.heuristic-input').forEach(input => {
        const nodeId = parseInt(input.dataset.nodeId);
        const param = input.dataset.param;
        const value = parseInt(input.value) || 0;
        const node = solution.find(s => s.id === nodeId);
        if (node) {
            node[param] = value;
        }
    });

    return solution;
}

async function submitSolution(userSolution, isTrainingMode, taskData) {
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');

    let url = isTrainingMode ? `${apiHost}/A/FifteenPuzzle/Train` : `${apiHost}/A/FifteenPuzzle/Test`;
    if (isTrainingMode) {
        const heuristic = taskData.settings?.heuristic || 1;
        url += `?heuristic=${heuristic}`;
    }
    const body = isTrainingMode ? JSON.stringify(taskData.problem) : JSON.stringify(userSolution);

    console.log('Отправка решения:', { url, body });

    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            },
            body
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Ошибка при отправке решения: ${response.status} ${errorText}`);
        }
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        return await response.json();
    } catch (error) {
        console.error('Ошибка в submitSolution:', error);
        throw error;
    }
}

function displaySolutionFeedback(taskData) {
    const messageElement = document.getElementById('solution-message');
    messageElement.style.display = 'block';
    messageElement.innerHTML = '';

    const userSolution = taskData.userSolution || [];
    const correctSolution = taskData.solution || [];
    let isCorrect = true;
    let errorCounts = { h: 0, f: 0, g: 0 };

    document.querySelectorAll('.heuristic-input').forEach(input => {
        input.classList.remove('error', 'success');
    });
    document.querySelectorAll('.tree-node').forEach(node => node.classList.remove('correct-node', 'error-node'));

    correctSolution.forEach(correctNode => {
        const nodeElement = document.querySelector(`.tree-node[data-node-id="${correctNode.id}"]`);
        const userNode = userSolution.find(u => u.id === correctNode.id);

        if (nodeElement) {
            nodeElement.classList.add('correct-node');
            ['h', 'f', 'g'].forEach(param => {
                const input = document.querySelector(`.heuristic-input[data-node-id="${correctNode.id}"][data-param="${param}"]`);
                if (input) {
                    const correctValue = correctNode[param];
                    const userValue = userNode ? userNode[param] : 0;
                    if (parseInt(userValue) === correctValue) {
                        input.classList.add('success');
                    } else {
                        input.classList.add('error');
                        errorCounts[param]++;
                        isCorrect = false;
                    }
                }
            });
        }
    });

    userSolution.forEach(userNode => {
        const correctNode = correctSolution.find(c => c.id === userNode.id);
        const nodeElement = document.querySelector(`.tree-node[data-node-id="${userNode.id}"]`);
        if (!correctNode && nodeElement) {
            nodeElement.classList.add('error-node');
            ['h', 'f', 'g'].forEach(param => {
                const input = document.querySelector(`.heuristic-input[data-node-id="${userNode.id}"][data-param="${param}"]`);
                if (input) {
                    input.classList.add('error');
                    errorCounts[param]++;
                    isCorrect = false;
                }
            });
        }
    });

    if (isCorrect) {
        messageElement.classList.remove('error');
        messageElement.classList.add('success');
        messageElement.innerHTML = 'Решение правильное! Все значения верны.';
    } else {
        messageElement.classList.remove('success');
        messageElement.classList.add('error');
        messageElement.innerHTML = `Найдены ошибки: ${errorCounts.h} неверных значений h, ${errorCounts.f} неверных значений f, ${errorCounts.g} неверных значений g.`;
    }
}

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

function drawLinesOnCanvas() {
    const canvas = document.getElementById('tree-canvas');
    if (!canvas) {
        console.error('Canvas не найден');
        return;
    }
    const ctx = canvas.getContext('2d');
    const treeContainer = document.getElementById('tree-container');
    const containerRect = treeContainer.getBoundingClientRect();

    canvas.width = treeContainer.scrollWidth;
    canvas.height = treeContainer.scrollHeight;
    canvas.style.width = `${treeContainer.scrollWidth}px`;
    canvas.style.height = `${treeContainer.scrollHeight}px`;

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = '#808080';
    ctx.lineWidth = 2;

    window.lines.forEach(line => {
        const childrenContainer = document.getElementById(line.containerId);
        if (childrenContainer && childrenContainer.style.display === 'inline-flex') {
            const startRect = line.start.getBoundingClientRect();
            const endRect = line.end.getBoundingClientRect();

            const startX = startRect.left - containerRect.left + treeContainer.scrollLeft + startRect.width / 2;
            const startY = startRect.bottom - containerRect.top + treeContainer.scrollTop;
            const endX = endRect.left - containerRect.left + treeContainer.scrollLeft + endRect.width / 2;
            const endY = endRect.top - containerRect.top + treeContainer.scrollTop;

            ctx.beginPath();
            ctx.moveTo(startX, startY);
            ctx.lineTo(endX, endY);
            ctx.stroke();
        }
    });
}