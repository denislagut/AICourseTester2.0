function restrictAccess() {
    const authToken = Cookies.get('.AspNetCore.Identity.Application');
    if (!authToken) {
        alert('Необходимо авторизоваться');
        window.location.href = '/LoginPage/LoginPage.html';
        return false;
    }
    const userFullName = sessionStorage.getItem('userFullName');
    if (!userFullName || sessionStorage.getItem('isTeacher') === null) {
        alert('Пожалуйста, войдите в систему');
        window.location.href = '/LoginPage/LoginPage.html';
        return false;
    }
    return true;
}

function isTokenExpired(token) {
    try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        const expiry = payload.exp * 1000;
        return Date.now() >= expiry;
    } catch {
        return true;
    }
}
async function refreshToken() {
    try {
        const refreshToken = Cookies.get('RefreshToken');
        if (!refreshToken) {
            throw new Error('Refresh token отсутствует');
        }

        const res = await fetch(`${apiHost}/Users/Refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken })
        });

        if (res.status === 200) {
            const result = await res.json();
            Cookies.set('.AspNetCore.Identity.Application', result.accessToken);
            if (result.refreshToken) {
                Cookies.set('RefreshToken', result.refreshToken);
            }
            return result.accessToken;
        } else {
            throw new Error('Не удалось обновить токен');
        }
    } catch (error) {
        console.error('Ошибка обновления токена:', error);
        window.location.href = '/LoginPage/LoginPage.html';
        return null;
    }
}