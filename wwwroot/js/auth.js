function showRegister() {
    document.getElementById('authScene').classList.add('show-register');
}

function showLogin() {
    document.getElementById('authScene').classList.remove('show-register');
}

const path = window.location.pathname.toLowerCase();
if (path.includes('register')) showRegister();

const eyeShow = `<svg fill="none" stroke="currentColor" stroke-width="1.8" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.477 0 8.268 2.943 9.542 7-1.274 4.057-5.065 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
    <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
</svg>`;

const eyeHide = `<svg fill="none" stroke="currentColor" stroke-width="1.8" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" d="M17.94 17.94A10.07 10.07 0 0112 19c-4.477 0-8.268-2.943-9.542-7a9.97 9.97 0 012.512-4.092m3.17-2.462A9.956 9.956 0 0112 5c4.477 0 8.268 2.943 9.542 7a9.966 9.966 0 01-4.293 5.411M3 3l18 18"/>
</svg>`;

function togglePw(inputId, btn) {
    const input = document.getElementById(inputId);
    const isHidden = input.type === 'password';
    input.type = isHidden ? 'text' : 'password';
    btn.innerHTML = isHidden ? eyeHide : eyeShow;
}

function showError(formId, message) {
    let errEl = document.getElementById(formId + '-error');
    if (!errEl) {
        errEl = document.createElement('div');
        errEl.id = formId + '-error';
        errEl.style.cssText = `
            margin-top: 14px;
            padding: 10px 14px;
            background: rgba(255, 79, 123, 0.1);
            border: 1px solid rgba(255, 79, 123, 0.35);
            border-radius: 6px;
            color: #ff4f7b;
            font-size: 13px;
            font-family: 'Inter', sans-serif;
            text-align: center;
        `;
        document.getElementById(formId).appendChild(errEl);
    }
    errEl.textContent = message;
}

function clearError(formId) {
    const errEl = document.getElementById(formId + '-error');
    if (errEl) errEl.textContent = '';
}

// AJAX Login 
//Gửi dữ liệu form qua fetch(AJAX)
document.getElementById('login-form').addEventListener('submit', async function (e) {
    e.preventDefault();
    clearError('login-form');

    const btn = this.querySelector('.btn');
    const originalText = btn.textContent;
    btn.textContent = 'Đang xử lý...';
    btn.disabled = true;

    try {
        const response = await fetch(this.action, {
            method: 'POST',
            body: new FormData(this),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.redirected) {
            window.location.href = response.url;
            return;
        }

        const data = await response.json().catch(() => null);
        if (response.ok) {
            window.location.href = data?.redirectUrl || '/';
        } else {
            showError('login-form', data?.message || 'Email hoặc mật khẩu không đúng.');
        }
    } catch {
        showError('login-form', 'Lỗi kết nối. Vui lòng thử lại.');
    }

    btn.textContent = originalText;
    btn.disabled = false;
});

// AJAX Register
//Gửi dữ liệu form qua fetch(AJAX)
document.getElementById('register-form').addEventListener('submit', async function (e) {
    e.preventDefault();
    clearError('register-form');

    const pw = document.getElementById('regPw').value;
    const confirm = document.getElementById('regConfirm').value;
    if (pw !== confirm) {
        showError('register-form', 'Mật khẩu nhập lại không khớp.');
        return;
    }

    const btn = this.querySelector('.btn');
    const originalText = btn.textContent;
    btn.textContent = 'Đang xử lý...';
    btn.disabled = true;

    try {
        const response = await fetch(this.action, {
            method: 'POST',
            body: new FormData(this),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.redirected) {
            window.location.href = response.url;
            return;
        }

        const data = await response.json().catch(() => null);
        if (response.ok) {
            window.location.href = data?.redirectUrl || '/';
        } else {
            showError('register-form', data?.message || 'Đăng ký thất bại. Vui lòng thử lại.');
        }
    } catch {
        showError('register-form', 'Lỗi kết nối. Vui lòng thử lại.');
    }

    btn.textContent = originalText;
    btn.disabled = false;
});