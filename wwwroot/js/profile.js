// profile.js
function previewAndSubmit(input) {
    if (!input.files[0]) return;
    const reader = new FileReader();
    reader.onload = e => {
        // Cập nhật avatar trong profile
        const wrap = document.querySelector('.pf-avatar-wrap');
        let img = document.getElementById('avatarPreview');
        if (!img) {
            img = document.createElement('img');
            img.id = 'avatarPreview';
            img.className = 'pf-avatar-img';
            img.alt = 'Avatar';
            const initial = document.getElementById('avatarInitial');
            if (initial) initial.replaceWith(img);
            else wrap.prepend(img);
        }
        img.src = e.target.result;

        // Cập nhật avatar trên Header luôn
        const headerAvatar = document.querySelector('.pc-avatar');
        if (headerAvatar) {
            const avatarImg = document.createElement('img');
            avatarImg.src = e.target.result;
            avatarImg.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:8px;';
            headerAvatar.innerHTML = '';
            headerAvatar.appendChild(avatarImg);
        }
    };
    reader.readAsDataURL(input.files[0]);
    document.getElementById('avatarForm').submit();
}

function togglePwd(id, btn) {
    const input = document.getElementById(id);
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    btn.textContent = show ? '🙈' : '👁';
}

function checkStrength(val) {
    const colors = ['#ff4f7b', '#ffaa00', '#00cfff', '#4cdf8a'];
    const labels = ['Yếu', 'Trung bình', 'Khá', 'Mạnh'];
    let score = 0;
    if (val.length >= 6) score++;
    if (val.length >= 10) score++;
    if (/[A-Z]/.test(val) && /[0-9]/.test(val)) score++;
    if (/[^A-Za-z0-9]/.test(val)) score++;

    [1, 2, 3, 4].forEach(i => {
        document.getElementById('bar' + i).style.background =
            i <= score ? colors[score - 1] : 'rgba(255,255,255,0.08)';
    });

    const lbl = document.getElementById('strengthLabel');
    lbl.textContent = val.length > 0 ? (labels[score - 1] || '') : '';
    lbl.style.color = val.length > 0 ? colors[score - 1] : '#6a6a9a';
    checkMatch();
}

function checkMatch() {
    const newVal = document.getElementById('newPwd').value;
    const cfm = document.getElementById('confirmPwd');
    if (!cfm.value) {
        cfm.style.borderColor = 'rgba(162,89,255,0.2)';
        cfm.style.boxShadow = 'none';
        return;
    }
    const ok = newVal === cfm.value;
    cfm.style.borderColor = ok ? '#4cdf8a' : '#ff4f7b';
    cfm.style.boxShadow = ok
        ? '0 0 0 3px rgba(76,223,138,0.1)'
        : '0 0 0 3px rgba(255,79,123,0.1)';
}