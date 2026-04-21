let taskData;

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
        
        if (isTrainingMode) {
            document.getElementById('training-mode').style.display = 'block';
            document.getElementById('tasksettings').style.display = 'block';
            document.getElementById('apply-settings').style.display = 'block';
        }

        taskData = await fetchTaskData(taskId, userId, isViewMode, isTrainingMode);
        if (!taskData || !taskData.problem) {
            alert('Данные задачи не найдены');
            if (userId) {
                window.location.href = "/ProfileTeacherPage/ProfileTeacherPage.html";
            } else {
                window.location.href = "/ProfileStudentPage/ProfileStudentPage.html";
            }
            return;
        }

        renderTree(taskData.problem.head, null, 0, taskData.userSolution, taskData.userPath, taskData.isSolved);
        resetEventListeners(isViewMode, isTrainingMode);

        if (isViewMode || taskData.isSolved) {
            document.querySelectorAll('.node-input input').forEach(input => {
                input.disabled = true;
            });
            const submitButton = document.getElementById('submit-solution');
            if (isViewMode && submitButton) {
                submitButton.style.display = 'none';
            } else if (taskData.isSolved && submitButton) {
                submitButton.style.display = 'block';
            }
            const solutionMessage = document.getElementById('solution-message');
            if (solutionMessage) {
                solutionMessage.style.display = 'block';
            }
            if (taskData.userSolution && taskData.userPath && taskData.correctSolution) {
                try {
                    const checkResult = checkSolution({
                        nodes: taskData.userSolution,
                        path: taskData.userPath
                    }, taskData.correctSolution, isTrainingMode);
                    console.log('Решение студента:', taskData.userSolution, taskData.userPath);
                    console.log('Результат проверки:', checkResult);

                    if (solutionMessage) {
                        solutionMessage.innerHTML = '';
                        if (checkResult.isCorrect) {
                            solutionMessage.classList.remove('error');
                            solutionMessage.classList.add('success');
                            solutionMessage.innerHTML = 'Все минимаксные значения, узлы, путь и отсечения выполнены правильно!';
                        } else {
                            solutionMessage.classList.remove('success');
                            solutionMessage.classList.add('error');
                            const valueErrors = checkResult.errors.filter(e => e.errorType === 'incorrect_value').length;
                            const extraNodeErrors = checkResult.errors.filter(e => e.errorType === 'extra_node').length;
                            const missingNodeErrors = checkResult.errors.filter(e => e.errorType === 'missing_node').length;
                            const incorrectlyPrunedErrors = checkResult.errors.filter(e => e.errorType === 'incorrectly_pruned').length;
                            const pathNotSelectedErrors = checkResult.errors.filter(e => e.errorType === 'path_not_selected').length;
                            const incorrectPathErrors = checkResult.errors.filter(e => e.errorType === 'incorrect_path').length;
                            let message = 'Найдены ошибки:';
                            message += '<ul>';
                            if (valueErrors > 0) {
                                message += `<li>Ошибка в определении минимаксных значений в ${valueErrors} узлах.</li>`;
                            }
                            if (extraNodeErrors > 0 || missingNodeErrors > 0) {
                                message += `<li>Ошибка в отсечении ${extraNodeErrors + missingNodeErrors} узлов.</li>`;
                            }
                            if (pathNotSelectedErrors > 0 || incorrectPathErrors > 0) {
                                message += `<li>Ошибка в выборе правильного пути.</li>`;
                            }
                            message += '</ul>';
                            solutionMessage.innerHTML = message;
                        }
                    }
                } catch (error) {
                    console.error('Ошибка проверки решения в режиме просмотра:', error);
                    if (solutionMessage) {
                        solutionMessage.style.display = 'block';
                        solutionMessage.classList.remove('success');
                        solutionMessage.classList.add('error');
                        solutionMessage.innerHTML = `Не удалось проверить решение: ${error.message}`;
                    }
                }
            } else {
                if (solutionMessage) {
                    solutionMessage.style.display = 'block';
                    solutionMessage.classList.remove('success');
                    solutionMessage.classList.add('error');
                    solutionMessage.innerHTML = 'Данные для проверки решения отсутствуют';
                }
            }
        }
    } catch (error) {
        console.error('Ошибка загрузки задачи:', error);
        alert('Не удалось загрузить задачу');
        if (userId) {
            window.location.href = "/ProfileTeacherPage/ProfileTeacherPage.html";
        } else {
            window.location.href = "/ProfileStudentPage/ProfileStudentPage.html";
        }
    }
});

async function fetchTaskData(taskId, userId, isViewMode, isTrainingMode, settings = null) {
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');

    let url;
    let response;

    if (isTrainingMode) {
        url = `${apiHost}/AB/Train`;
        if (settings) {
            const params = new URLSearchParams({
                depth: settings.depth,
                max: settings.max,
                template: settings.template
            });
            url += `?${params.toString()}`;
        }
        response = await fetch(url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (!response.ok) {
            throw new Error(`Ошибка при получении тренировочной задачи: ${response.status} ${response.statusText}`);
        }
        if (response.status === 401) {
            const refreshtoken = Cookies.get('RefreshToken');
            if (isTokenExpired(authtoken)) {
                refreshToken();
            }
        }
        const data = await response.json();
        if (!data.head) {
            throw new Error('Данные задачи отсутствуют в ответе');
        }

        return {
            id: null,
            problem: { head: data.head },
            userSolution: null,
            userPath: null,
            isSolved: false,
            date: null,
            correctSolution: null
        };
    }
    if (isViewMode) {
        url = `${apiHost}/AB/Users/${userId}`;
        response = await fetch(url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (!response.ok) {
            throw new Error(`Ошибка при получении данных пользователя: ${response.status} ${response.statusText}`);
        }

        const data = await response.json();
        if (!data.task || !data.task.problem) {
            throw new Error('Данные задачи отсутствуют в ответе');
        }

        if (isViewMode && data.user) {
            const studentInfo = document.getElementById('student-info');
            if (studentInfo) {
                const fullName = [data.user.secondName, data.user.name, data.user.patronymic]
                    .filter(Boolean)
                    .join(' ');
                studentInfo.textContent = `Решение студента: ${fullName || 'Неизвестный студент'}`;
                studentInfo.style.display = 'block';
            }
        }

        return {
            id: data.task.id,
            problem: data.task.problem,
            userSolution: data.task.userSolution,
            userPath: data.task.userPath,
            isSolved: data.task.isSolved,
            date: data.task.date,
            correctSolution: { nodes: data.task.solution, path: data.task.path } || { nodes: [], path: [] }
        };
    } else {
        url = `${apiHost}/AB/Test`;
        response = await fetch(url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${authtoken}`
            }
        });

        if (!response.ok) {
            throw new Error(`Ошибка при получении задачи: ${response.status} ${response.statusText}`);
        }

        const data = await response.json();
        if (!data.problem) {
            throw new Error('Данные задачи отсутствуют в ответе');
        }

        return {
            id: data.id,
            problem: data.problem,
            userSolution: data.userSolution,
            userPath: data.userPath,
            isSolved: data.isSolved,
            date: data.date,
            correctSolution: { nodes: data.solution, path: data.path } || { nodes: [], path: [] }
        };
    }
}

function resetEventListeners(isViewMode = false, isTrainingMode = false) {
    // Удаляем существующие обработчики событий, чтобы избежать дублирования
    const submitButton = document.getElementById('submit-solution');
    const applySettingsButton = document.getElementById('apply-settings');
    const taskDisplay = document.querySelector('.section-tasksolve__task-display');
    const logoutButton = document.getElementById('profile-tooltip__button-logout');
    
    // Клонируем элементы, чтобы удалить старые обработчики
    if (submitButton) {
        const newSubmitButton = submitButton.cloneNode(true);
        submitButton.parentNode.replaceChild(newSubmitButton, submitButton);
    }
    if (applySettingsButton) {
        const newApplySettingsButton = applySettingsButton.cloneNode(true);
        applySettingsButton.parentNode.replaceChild(newApplySettingsButton, applySettingsButton);
    }
    if (taskDisplay) {
        const newTaskDisplay = taskDisplay.cloneNode(true);
        taskDisplay.parentNode.replaceChild(newTaskDisplay, taskDisplay);
    }
    
    // Устанавливаем новые обработчики
    if (!isViewMode) {
        document.getElementById('submit-solution').addEventListener('click', async function() {
            const userSolution = collectSolution();
            const messageElement = document.getElementById('solution-message');
            try {
                const response = await submitSolution(userSolution, isTrainingMode, taskData.problem);
                const checkResult = checkSolution(userSolution, response, isTrainingMode);
                console.log('Решение пользователя:', userSolution);
                console.log('Результат проверки:', checkResult);
                messageElement.style.display = 'block';
                messageElement.innerHTML = '';
                if (checkResult.isCorrect) {
                    messageElement.classList.remove('error');
                    messageElement.classList.add('success');
                    messageElement.innerHTML = 'Все минимаксные значения, узлы, путь и отсечения выполнены правильно!';
                } else {
                    messageElement.classList.remove('success');
                    messageElement.classList.add('error');
                    const valueErrors = checkResult.errors.filter(e => e.errorType === 'incorrect_value').length;
                    const extraNodeErrors = checkResult.errors.filter(e => e.errorType === 'extra_node').length;
                    const missingNodeErrors = checkResult.errors.filter(e => e.errorType === 'missing_node').length;
                    const incorrectlyPrunedErrors = checkResult.errors.filter(e => e.errorType === 'incorrectly_pruned').length;
                    const pathNotSelectedErrors = checkResult.errors.filter(e => e.errorType === 'path_not_selected').length;
                    const incorrectPathErrors = checkResult.errors.filter(e => e.errorType === 'incorrect_path').length;
                    let message = 'Найдены ошибки:';
                    message += '<ul>';
                    if (valueErrors > 0) {
                        message += `<li>Ошибка в определении минимаксных значений в ${valueErrors} узлах.</li>`;
                    }
                    if (extraNodeErrors > 0 || missingNodeErrors > 0) {
                        message += `<li>Ошибка в отсечении ${extraNodeErrors + missingNodeErrors} узлов.</li>`;
                    }
                    if (pathNotSelectedErrors > 0 || incorrectPathErrors > 0) {
                        message += `<li>Ошибка в выборе правильного пути.</li>`;
                    }
                    message += '</ul>';
                    messageElement.innerHTML = message;
                }
            } catch (error) {
                console.error('Ошибка отправки решения:', error);
                messageElement.style.display = 'block';
                messageElement.classList.remove('success');
                messageElement.classList.add('error');
                messageElement.innerHTML = `Не удалось проверить решение: ${error.message}`;
            }
        });

        if (isTrainingMode) {
            document.getElementById('apply-settings').addEventListener('click', async function() {
                const treeHeight = document.getElementById('tree-height-input').value;
                const max = document.getElementById('max-input').value;
                const template = document.getElementById('template-input').value;

                if (!treeHeight || !max || !template || treeHeight < 1 || max < 1 || template < 1) {
                    alert('Пожалуйста, заполните все поля настроек корректными значениями');
                    return;
                }

                try {
                    const settings = {
                        depth: parseInt(treeHeight),
                        max: parseInt(max),
                        template: parseInt(template)
                    };
                    taskData = await fetchTaskData(null, null, false, true, settings); // Обновляем taskData
                    if (!taskData || !taskData.problem) {
                        throw new Error('Не удалось получить новое задание');
                    }

                    const solutionMessage = document.getElementById('solution-message');
                    if (solutionMessage) {
                        solutionMessage.style.display = 'none';
                        solutionMessage.innerHTML = '';
                    }

                    renderTree(taskData.problem.head, null, 0, null, null, false);
                    resetEventListeners(isViewMode, isTrainingMode); // Перерегистрируем обработчики с новым taskData
                } catch (error) {
                    console.error('Ошибка применения настроек:', error);
                    alert(`Не удалось применить настройки: ${error.message}`);
                }
            });
        }

        document.querySelector('.section-tasksolve__task-display').addEventListener('click', function(e) {
            if (e.target.tagName === 'line' && (e.target.classList.contains('branch-line') || e.target.classList.contains('hit-area'))) {
                e.preventDefault();
                const line = e.target.classList.contains('hit-area') ? e.target.previousSibling : e.target;
                const currentColor = line.getAttribute('stroke');
                
                if (currentColor === '#f44336' || currentColor === '#4CAF50') {
                    line.setAttribute('stroke', '#808080');
                } else {
                    line.setAttribute('stroke', '#f44336');
                }
            } else if (e.target.classList.contains('tree-node')) {
                e.preventDefault();
                let node = e.target;
                let id = parseInt(node.dataset.nodeId);
                let line = document.querySelector(`.branch-line[data-child-id="${id}"]`);
                if (line === null) {
                    return;
                }  
                let currColor = line.getAttribute('stroke') === '#f44336' ? '#808080' : '#f44336';
                line.setAttribute('stroke', currColor);

                currColor = line.getAttribute('stroke') === '#808080' ? '#808080' : '#f44336';
                id = line.dataset.childId;
                let lines = document.querySelectorAll(`.branch-line[data-parent-id="${id}"]`);
                if (lines === null) {
                    return;
                }
                lines.forEach(line => {
                    line.setAttribute('stroke', currColor);
                });
            }
        });

        document.querySelector('.section-tasksolve__task-display').addEventListener('contextmenu', function(e) {
            if (e.target.tagName === 'INPUT') {
                return;
            }
            e.preventDefault();
            if (e.target.tagName === 'line' && (e.target.classList.contains('branch-line') || e.target.classList.contains('hit-area'))) {
                const line = e.target.classList.contains('hit-area') ? e.target.previousSibling : e.target;
                const parentId = parseInt(line.dataset.parentId);
                const childId = parseInt(line.dataset.childId);
                const currentColor = line.getAttribute('stroke');
                
                if (currentColor === '#f44336' || currentColor === '#4CAF50') {
                    line.setAttribute('stroke', '#808080');
                } else {
                    let levelOneNodeId;
                    if (parentId === 0) {
                        levelOneNodeId = childId;
                    } else {
                        const parentLine = document.querySelector(`.branch-line[data-child-id="${parentId}"]`);
                        if (parentLine) {
                            levelOneNodeId = parseInt(parentLine.dataset.childId);
                        }
                    }
                    
                    if (levelOneNodeId) {
                        document.querySelectorAll('.branch-line').forEach(otherLine => {
                            const otherChildId = parseInt(otherLine.dataset.childId);
                            const otherParentId = parseInt(otherLine.dataset.parentId);
                            let otherLevelOneNodeId;
                            if (otherParentId === 0) {
                                otherLevelOneNodeId = otherChildId;
                            } else {
                                const otherParentLine = document.querySelector(`.branch-line[data-child-id="${otherParentId}"]`);
                                if (otherParentLine) {
                                    otherLevelOneNodeId = parseInt(otherParentLine.dataset.childId);
                                }
                            }
                            if (otherLevelOneNodeId && otherLevelOneNodeId !== levelOneNodeId && otherLine.getAttribute('stroke') === '#4CAF50') {
                                otherLine.setAttribute('stroke', '#808080');
                            }
                        });
                    }
                    
                    document.querySelectorAll(`.branch-line[data-parent-id="${parentId}"]`).forEach(siblingLine => {
                        if (siblingLine !== line && siblingLine.getAttribute('stroke') === '#4CAF50') {
                            siblingLine.setAttribute('stroke', '#808080');
                        }
                    });
                    
                    line.setAttribute('stroke', '#4CAF50');
                }
            } else if (e.target.classList.contains('tree-node')) {
                let node = e.target;
                let id = parseInt(node.dataset.nodeId);
                let lines = [document.querySelector(`.branch-line[data-child-id="${id}"]`)];
                if (lines[0] === null) {
                    return;
                }
                lines.push(document.querySelector(`.branch-line[data-child-id="${lines[0].dataset.parentId}"]`));
                let prevLineColor = null;
                lines.forEach(line => {
                    if (line === null) {
                        document.querySelectorAll(`.branch-line[data-parent-id="${node.dataset.nodeId}"]`).forEach(
                            line => {
                                if (line.getAttribute('stroke') === '#4CAF50') {
                                    line.setAttribute('stroke', '#808080');
                                }
                            }
                        );
                        return;
                    }
                    let currColor = line.getAttribute('stroke');
                    if (currColor === prevLineColor || prevLineColor && currColor === '#4CAF50') {
                        return;
                    }
                    const parentId = parseInt(line.dataset.parentId);
                    const childId = parseInt(line.dataset.childId);
                    const currentColor = line.getAttribute('stroke');
                    
                    if (currentColor === '#4CAF50') {
                        line.setAttribute('stroke', '#808080');
                    } else {
                        let levelOneNodeId;
                        if (parentId === 0) {
                            levelOneNodeId = childId;
                        } else {
                            const parentLine = document.querySelector(`.branch-line[data-child-id="${parentId}"]`);
                            if (parentLine) {
                                levelOneNodeId = parseInt(parentLine.dataset.childId);
                            }
                        }
                        
                        if (levelOneNodeId) {
                            document.querySelectorAll('.branch-line').forEach(otherLine => {
                                const otherChildId = parseInt(otherLine.dataset.childId);
                                const otherParentId = parseInt(otherLine.dataset.parentId);
                                let otherLevelOneNodeId;
                                if (otherParentId === 0) {
                                    otherLevelOneNodeId = otherChildId;
                                } else {
                                    const otherParentLine = document.querySelector(`.branch-line[data-child-id="${otherParentId}"]`);
                                    if (otherParentLine) {
                                        otherLevelOneNodeId = parseInt(otherParentLine.dataset.childId);
                                    }
                                }
                                if (otherLevelOneNodeId && otherLevelOneNodeId !== levelOneNodeId && otherLine.getAttribute('stroke') === '#4CAF50') {
                                    otherLine.setAttribute('stroke', '#808080');
                                }
                            });
                        }
                        
                        document.querySelectorAll(`.branch-line[data-parent-id="${parentId}"]`).forEach(siblingLine => {
                            if (siblingLine !== line && siblingLine.getAttribute('stroke') === '#4CAF50') {
                                siblingLine.setAttribute('stroke', '#808080');
                            }
                        });
                        
                        line.setAttribute('stroke', '#4CAF50');
                    }
                    prevLineColor = line.getAttribute('stroke');
                });
            }
        });

        document.addEventListener('dblclick', function(e) {
            if (e.target.tagName === 'line' && (e.target.classList.contains('branch-line') || e.target.classList.contains('hit-area'))) {
                e.preventDefault();
            }
        });
    }

    logoutButton.addEventListener('click', function(e) {
        e.preventDefault();
        Logout();
    });
}

async function submitSolution(userSolution, isTrainingMode, problem) {
    if (!userSolution.nodes.every(node => Number.isInteger(node.id) && Number.isInteger(node.a) && Number.isInteger(node.b))) {
        throw new Error('Некорректные данные в узлах');
    }
    if (!userSolution.path.every(id => Number.isInteger(id))) {
        throw new Error('Некорректные данные в пути');
    }
    if (userSolution.path.length === 0) {
        throw new Error('Путь не выбран. Выделите зеленым ветви от корня до листа.');
    }
    
    const authtoken = Cookies.get('.AspNetCore.Identity.Application');

    const url = isTrainingMode ? `${apiHost}/AB/Train` : `${apiHost}/AB/Test`;
    const body = isTrainingMode ? JSON.stringify({ head: problem.head, nodes: userSolution.nodes, path: userSolution.path }) : JSON.stringify({
        nodes: userSolution.nodes,
        path: userSolution.path
    });

    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${authtoken}`
        },
        body: body
    });
    
    if (!response.ok) {
        const errorText = await response.text();
        console.error('Ошибка сервера:', response.status, errorText);
        throw new Error(`Ошибка при отправке решения: ${response.status} ${errorText}`);
    }
    if (response.status === 401) {
        const refreshtoken = Cookies.get('RefreshToken');
        if (isTokenExpired(authtoken)) {
            refreshToken();
        }
    }
    const responseData = await response.json();
    console.log('Ответ сервера:', responseData);
    
    if (!responseData || !Array.isArray(responseData.nodes) || !Array.isArray(responseData.path)) {
        console.error('Некорректный формат ответа сервера:', responseData);
        throw new Error('Некорректный формат ответа сервера');
    }
    
    return responseData;
}

function checkSolution(userSolution, correctSolution, isTrainingMode = false) {
    const errors = [];
    const solutionMap = new Map(correctSolution.nodes.map(node => [node.id, node]));
    const correctPath = new Set(correctSolution.path);
    const correctNodeIds = new Set(correctSolution.nodes.map(node => node.id));
    const userNodeIds = new Set(userSolution.nodes.map(node => node.id));
    const userPath = new Set(userSolution.path);
    
    const redBranchIds = new Set();
    const greenBranchIds = new Set();
    const levelOneIds = new Set();
    const levelTwoIds = new Set();
    
    document.querySelectorAll('.branch-line').forEach(line => {
        const childId = parseInt(line.dataset.childId);
        const parentId = parseInt(line.dataset.parentId);
        const stroke = line.getAttribute('stroke');
        if (stroke === '#f44336') {
            redBranchIds.add(childId);
        } else if (stroke === '#4CAF50') {
            greenBranchIds.add(childId);
        }
        if (parentId === 0) {
            levelOneIds.add(childId);
        } else if (levelOneIds.has(parentId)) {
            levelTwoIds.add(childId);
        }
    });
    
    userSolution.nodes.forEach(node => {
        const nodeElementDom = document.querySelector(`.tree-node[data-node-id="${node.id}"]`);
        if (nodeElementDom) {
            nodeElementDom.classList.remove('pruned-node', 'correct-path-node', 'incorrectly-pruned-node');
        }
        
        if (!node.isLeaf) {
            const correctNode = solutionMap.get(node.id);
            if (correctNode) {
                const isRoot = node.id === 0;
                const userValue = isRoot ? node.a : node.b;
                const correctValue = isRoot ? correctNode.a : correctNode.b;
                if (userValue !== correctValue) {
                    errors.push({
                        id: node.id,
                        input: userValue,
                        correct: correctValue,
                        type: isRoot ? 'α' : 'β',
                        errorType: 'incorrect_value'
                    });
                    const input = document.querySelector(`input[data-node-id="${node.id}"]`);
                    if (input) input.classList.add('error-input');
                } else {
                    const input = document.querySelector(`input[data-node-id="${node.id}"]`);
                    if (input) input.classList.remove('error-input');
                }
            }
        }
    });
    
    document.querySelectorAll('.tree-node').forEach(nodeElement => {
        const nodeId = parseInt(nodeElement.dataset.nodeId);
        const nodeElementDom = document.querySelector(`.tree-node[data-node-id="${nodeId}"]`);
        const isPrunedByUser = redBranchIds.has(nodeId);
        const isSelectedByUser = greenBranchIds.has(nodeId);
        const isLevelTwo = levelTwoIds.has(nodeId);
        const isLeaf = nodeElement.classList.contains('leaf-node');
        
        if (nodeElementDom && !userNodeIds.has(nodeId)) {
            nodeElementDom.classList.remove('pruned-node', 'correct-path-node', 'incorrectly-pruned-node');
        }
        
        if (nodeId === 0) {
            if (nodeElementDom) {
                nodeElementDom.classList.add('correct-path-node');
            }
        } else if (correctNodeIds.has(nodeId) && userNodeIds.has(nodeId)) {
            const correctNode = solutionMap.get(nodeId);
            const userNode = userSolution.nodes.find(n => n.id === nodeId);
            const isRoot = nodeId === 0;
            let userValue = isRoot ? userNode.a : userNode.b;
            let correctValue = isRoot ? correctNode.a : correctNode.b;
            
            if (isLeaf) {
                userValue = userNode.a; // Для листовых узлов используем только a
                correctValue = correctNode.a;
            }
            
            if (correctPath.has(nodeId) && !userPath.has(nodeId) && !isPrunedByUser) {
                errors.push({
                    id: nodeId,
                    input: null,
                    correct: null,
                    type: 'path',
                    errorType: 'path_not_selected'
                });
                if (nodeElementDom) {
                    nodeElementDom.classList.add('pruned-node');
                }
            } else if (!correctPath.has(nodeId) && isSelectedByUser) {
                errors.push({
                    id: nodeId,
                    input: null,
                    correct: null,
                    type: 'path',
                    errorType: 'incorrect_path'
                });
                if (nodeElementDom) {
                    nodeElementDom.classList.add('pruned-node');
                }
            } else if (isPrunedByUser && !correctPath.has(nodeId)) {
                if (isLevelTwo) {
                    errors.push({
                        id: nodeId,
                        input: null,
                        correct: null,
                        type: 'pruning',
                        errorType: 'incorrectly_pruned'
                    });
                    if (nodeElementDom) {
                        nodeElementDom.classList.add('incorrectly-pruned-node');
                    }
                }
            } else if ((isLeaf && !isPrunedByUser && correctNodeIds.has(nodeId)) || 
                       (!isLeaf && userValue === correctValue && (!isPrunedByUser || (isTrainingMode && correctNodeIds.has(nodeId))))) {
                if (nodeElementDom) {
                    nodeElementDom.classList.add('correct-path-node');
                }
            }
        } else if (userNodeIds.has(nodeId) && !correctNodeIds.has(nodeId)) {
            errors.push({
                id: nodeId,
                input: null,
                correct: null,
                type: 'pruning',
                errorType: 'extra_node'
            });
            if (nodeElementDom) {
                nodeElementDom.classList.add('pruned-node');
            }
        } else if (correctNodeIds.has(nodeId) && !userNodeIds.has(nodeId)) {
            errors.push({
                id: nodeId,
                input: null,
                correct: null,
                type: 'pruning',
                errorType: 'missing_node'
            });
            if (nodeElementDom) {
                nodeElementDom.classList.add('pruned-node');
            }
        }
        
        if (correctPath.has(nodeId) && isPrunedByUser) {
            errors.push({
                id: nodeId,
                input: null,
                correct: null,
                type: 'pruning',
                errorType: 'incorrectly_pruned'
            });
            if (nodeElementDom) {
                nodeElementDom.classList.add('incorrectly-pruned-node');
            }
        } else if (!correctNodeIds.has(nodeId) && isPrunedByUser && !userNodeIds.has(nodeId)) {
            if (nodeElementDom) {
                nodeElementDom.classList.add('correct-path-node');
            }
        }
    });
    document.querySelectorAll('.branch-line').forEach(line => {
        if (line.getAttribute('stroke') === '#ffe10d') {
            line.setAttribute('stroke', '#808080');
        }
    });
    greenBranchIds.forEach(id => {
        if (!correctPath.has(id)) {
            document.querySelector(`.branch-line[data-child-id="${id}"]`).setAttribute('stroke', '#ffe10d');
        }
    });
    correctPath.forEach(id => {
        document.querySelector(`.branch-line[data-child-id="${id}"]`).setAttribute('stroke', '#4CAF50');
    });
    
    return {
        isCorrect: errors.length === 0,
        errors: errors
    };
}

function renderTree(node, parentContainer = null, level = 0, userSolution = null, userPath = null, isSolved = false) {
    const container = parentContainer || document.getElementById('tree-container');
    if (!parentContainer) container.innerHTML = '';

    const nodeWrapper = document.createElement('div');
    nodeWrapper.className = `node-wrapper level-${level}`;
    
    const nodeElement = document.createElement('div');
    nodeElement.className = 'tree-node';
    nodeElement.dataset.nodeId = node.id;
    
    if (!node.subNodes || node.subNodes.length === 0) {
        nodeElement.textContent = node.a;
        nodeElement.classList.add('leaf-node');
    } else {
        let inputValue = '';
        if (isSolved && userSolution) {
            const userNode = userSolution.find(n => n.id === node.id);
            if (userNode) {
                inputValue = node.id === 0 ? userNode.a : userNode.b;
            }
        }
        nodeElement.innerHTML = `
            <div class="node-input">
                <input type="text" data-node-id="${node.id}" value="${inputValue}" placeholder="${node.id === 0 ? '' : ''}">
            </div>
        `;
    }
    
    nodeWrapper.appendChild(nodeElement);
    container.appendChild(nodeWrapper);

    if (node.subNodes && node.subNodes.length > 0) {
        const childrenContainer = document.createElement('div');
        childrenContainer.className = 'children-container';
        nodeWrapper.appendChild(childrenContainer);
        
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.className = 'branch-svg';
        svg.style.position = 'absolute';
        svg.style.top = '0';
        svg.style.left = level === 0 ? '5px' : '0';
        svg.style.width = '100%';
        svg.style.height = '50px';
        svg.style.zIndex = '1';
        childrenContainer.appendChild(svg);
        
        const childCount = node.subNodes.length;
        const nodeWidth = level === 0 ? 280 : 60;
        const totalWidth = childCount * nodeWidth;
        const spacing = childCount === 1 ? 0 : totalWidth / childCount; 
        
        const userNodeIds = isSolved && userSolution ? new Set(userSolution.map(n => n.id)) : new Set();
        const userPathSet = isSolved && userPath ? new Set(userPath) : new Set();

        console.log(`Rendering node ${node.id} at level ${level}, userPath:`, userPath, 'userNodeIds:', Array.from(userNodeIds));

        node.subNodes.forEach((childNode, index) => {
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.classList.add('branch-line');
            line.dataset.parentId = node.id;
            line.dataset.childId = childNode.id;
            
            let strokeColor = '#808080';
            if (isSolved && userPath && userSolution) {
                if (userPathSet.has(childNode.id)) {
                    strokeColor = '#4CAF50';
                } else if (!userNodeIds.has(childNode.id)) {
                    strokeColor = '#f44336';
                }
            }

            console.log(`Branch ${node.id} -> ${childNode.id}: color=${strokeColor}, inPath=${userPathSet.has(childNode.id)}, inSolution=${userNodeIds.has(childNode.id)}`);

            line.setAttribute('stroke', strokeColor);
            line.setAttribute('stroke-width', '4');
            line.style.cursor = 'pointer';
            
            const hitArea = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            hitArea.classList.add('hit-area');
            hitArea.setAttribute('stroke', 'transparent');
            hitArea.setAttribute('stroke-width', '10');
            hitArea.style.cursor = 'pointer';
            
            const x1 = '50%';
            const y1 = 0;
            
            const offset = (index * spacing) - (totalWidth - nodeWidth) / 2;
            const x2 = `calc(50% + ${offset}px)`;
            const y2 = 50;
            
            line.setAttribute('x1', x1);
            line.setAttribute('y1', y1);
            line.setAttribute('x2', x2);
            line.setAttribute('y2', y2);
            hitArea.setAttribute('x1', x1);
            hitArea.setAttribute('y1', y1);
            hitArea.setAttribute('x2', x2);
            hitArea.setAttribute('y2', y2);
            
            svg.appendChild(line);
            svg.appendChild(hitArea);
            renderTree(childNode, childrenContainer, level + 1, userSolution, userPath, isSolved);
        });
    }
}

function collectSolution() {
    const solution = {
        nodes: [],
        path: []
    };
    
    const emptyNodes = [];
    const invalidNodes = [];
    document.querySelectorAll('.tree-node:not(.leaf-node)').forEach(nodeElement => {
        const nodeId = parseInt(nodeElement.dataset.nodeId);
        const input = nodeElement.querySelector('.node-input input');
        if (!input || input.value.trim() === '' || input.value <= 0) {
            emptyNodes.push(nodeId);
        } else {
            const value = input.value.trim();
            const num = parseInt(value);
            if (isNaN(num) || num.toString() !== value) {
                invalidNodes.push(nodeId);
            }
        }
    });
    
    if (emptyNodes.length > 0) {
        alert(`Пожалуйста, введите натуральные значения для всех узлов`);
        throw new Error('Не все узлы заполнены');
    }
    
    if (invalidNodes.length > 0) {
        alert(`Узлы могу содержать только целые числа`);
        throw new Error('Введены нецелые числа');
    }
    const redBranchChildIds = new Set();
    document.querySelectorAll('.branch-line').forEach(line => {
        const childId = parseInt(line.dataset.childId);
        const stroke = line.getAttribute('stroke');
        if (stroke === '#f44336') {
            redBranchChildIds.add(childId);
        }
    });
    
    document.querySelectorAll('.tree-node').forEach(nodeElement => {
        const nodeId = parseInt(nodeElement.dataset.nodeId);
        if (redBranchChildIds.has(nodeId)) return;
        
        const isLeaf = nodeElement.classList.contains('leaf-node');
        
        let a = null;
        let b = null;
        
        if (isLeaf) {
            a = convertInputValue(nodeElement.textContent);
            b = a;
        } else if (nodeId === 0) {
            a = convertInputValue(nodeElement.querySelector('.node-input input').value);
            b = 2147483647;
        } else {
            b = convertInputValue(nodeElement.querySelector('.node-input input').value);
            a = -2147483648;
        }
        
        if (a === null || b === null) {
            console.warn(`Некорректные значения для узла ${nodeId}: a=${a}, b=${b}`);
        }
        
        solution.nodes.push({
            id: nodeId,
            a: a !== null ? a : (isLeaf ? 0 : nodeId === 0 ? 0 : -2147483648),
            b: b !== null ? b : (isLeaf ? 0 : nodeId === 0 ? 2147483647 : 0)
        });
    });
    
    let currentId = 0;
    while (true) {
        const greenLine = Array.from(document.querySelectorAll('.branch-line')).find(line => 
            parseInt(line.dataset.parentId) === currentId && 
            line.getAttribute('stroke') === '#4CAF50'
        );
        
        if (!greenLine) break;
        
        const childId = parseInt(greenLine.dataset.childId);
        solution.path.push(childId);
        currentId = childId;
        
        const currentNode = document.querySelector(`.tree-node[data-node-id="${currentId}"]`);
        if (currentNode && currentNode.classList.contains('leaf-node')) break;
    }
    if (solution.path.length !== 2) {
        alert('Пожалуйста, выберите полный путь, выделив зеленым ветви от корня до листа.');
        throw new Error('Неполный путь');
    }
    console.log('Собранное решение:', solution);
    return solution;
}

function convertInputValue(value) {
    if (value === '' || value == null) return null;
    const num = parseInt(value);
    return isNaN(num) ? null : num;
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

