let selectedUserId = null;
let selectedUserName = '';
const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

async function loadUsers() {
    try {
        const res = await fetch('/Chat/StaffGetUsers');
        const users = await res.json();
        const body = document.getElementById('userListBody');

        if (users.length === 0) {
            body.innerHTML = '<div class="sc-no-users">Chưa có khách nào nhắn tin</div>';
            return;
        }

        body.innerHTML = users.map(u => `
            <div class="sc-user-item ${u.userId === selectedUserId ? 'active' : ''}"
                 onclick="selectUser('${u.userId}','${escHtml(u.username)}')">
                <div class="sc-u-avatar">${u.username[0].toUpperCase()}</div>
                <div class="sc-u-info">
                    <div class="sc-u-name">${escHtml(u.username)}</div>
                    <div class="sc-u-preview">${escHtml(u.lastMessage)}</div>
                </div>
                <div style="display:flex;flex-direction:column;align-items:flex-end;gap:4px">
                    <div class="sc-u-time">${u.lastTime}</div>
                    ${u.unread > 0 ? `<span class="sc-u-unread">${u.unread}</span>` : ''}
                </div>
            </div>
        `).join('');
    } catch (error) {
        console.error('Load users error:', error);
    }
}

async function selectUser(userId, username) {
    selectedUserId = userId;
    selectedUserName = username;
    loadUsers();

    const panel = document.getElementById('scChatPanel');
    panel.innerHTML = `
        <div class="sc-chat-hdr">
            <div class="sc-u-avatar">${username[0].toUpperCase()}</div>
            <div>
                <div class="sc-chat-title">${escHtml(username)}</div>
                <div class="sc-chat-sub">Đang hỗ trợ</div>
            </div>
        </div>
        <div class="sc-msgs" id="scMsgs">
            <div style="text-align:center;color:#6a6a9a;font-size:14px;padding:24px">Đang tải...</div>
        </div>
        <div class="sc-input-row">
            <input type="text" class="sc-input" id="scInput"
                   placeholder="Nhập tin nhắn hỗ trợ..."
                   onkeydown="if(event.key==='Enter') scSend()" />
            <button class="sc-send" onclick="scSend()">Gửi ➤</button>
        </div>
    `;

    await loadStaffMsgs();
}

async function loadStaffMsgs() {
    if (!selectedUserId) return;
    try {
        const res = await fetch(`/Chat/StaffGetMessages?userId=${selectedUserId}`);
        const msgs = await res.json();
        const container = document.getElementById('scMsgs');
        if (!container) return;

        container.innerHTML = msgs.map(m => {
            const cls = m.isFromUser ? 'from-user' : (m.isFromAI ? 'from-ai' : 'from-staff');
            const label = m.isFromUser ? '👤 Khách' : (m.isFromAI ? '🤖 AI' : '👨‍💼 Bạn');
            return `
                <div class="smsg ${cls}">
                    <div class="smsg-sender">${label}</div>
                    <div class="sbubble">${escHtml(m.content)}</div>
                    <div class="smeta">${m.time}</div>
                </div>
            `;
        }).join('');

        container.scrollTop = container.scrollHeight;
    } catch (error) {
        console.error('Load messages error:', error);
    }
}

async function scSend() {
    const input = document.getElementById('scInput');
    if (!input || !selectedUserId) return;
    const msg = input.value.trim();
    if (!msg) return;
    input.value = '';

    try {
        await fetch('/Chat/StaffReply', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': csrfToken
            },
            body: JSON.stringify({ userId: selectedUserId, content: msg })
        });
        await loadStaffMsgs();
    } catch (error) {
        console.error('Send error:', error);
    }
}

function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

// Load trạng thái online khi vào trang
async function loadMyStatus() {
    try {
        const res = await fetch('/Chat/GetMyStatus');
        const data = await res.json();
        const toggle = document.getElementById('onlineToggle');
        const label = document.getElementById('statusLabel');
        toggle.checked = data.isOnline;
        label.textContent = data.isOnline ? 'Online' : 'Offline';
        label.style.color = data.isOnline ? '#4cdf8a' : '#6a6a9a';
    } catch (error) {
        console.error('Load status error:', error);
    }
}

async function setOnlineStatus(isOnline) {
    try {
        await fetch('/Chat/SetOnlineStatus', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(isOnline)
        });
        const label = document.getElementById('statusLabel');
        label.textContent = isOnline ? 'Online' : 'Offline';
        label.style.color = isOnline ? '#4cdf8a' : '#6a6a9a';
    } catch (error) {
        console.error('Set status error:', error);
    }
}

// Tự động set offline khi đóng tab
window.addEventListener('beforeunload', () => {
    navigator.sendBeacon('/Chat/SetOnlineStatus',
        new Blob([JSON.stringify(false)], { type: 'application/json' })
    );
});

// Khởi tạo
document.addEventListener('DOMContentLoaded', () => {
    loadUsers();
    loadMyStatus();

    // Set intervals
    setInterval(loadUsers, 10000);
    setInterval(() => {
        if (selectedUserId) loadStaffMsgs();
    }, 4000);
});