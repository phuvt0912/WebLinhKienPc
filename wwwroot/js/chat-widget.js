let chatOpen = false;
let lastMsgCount = 0;

function toggleChat() {
    chatOpen = !chatOpen;
    const box = document.getElementById('chatBox');
    box.style.display = chatOpen ? 'block' : 'none';
    document.getElementById('fabIcon').textContent = chatOpen ? '✕' : '💬';
    if (chatOpen) {
        document.getElementById('fabBadge').style.display = 'none';
        loadChatHistory();
    }
}

async function loadChatHistory() {
    try {
        const res = await fetch('/Chat/GetMessages');
        const msgs = await res.json();
        const container = document.getElementById('chatMsgs');
        container.innerHTML = '';
        if (msgs.length === 0) {
            appendMsg('Xin chào! Tôi là trợ lý AI của LinhKienPC. Bạn cần hỗ trợ gì?', 'ai', 'Bây giờ', false);
        } else {
            msgs.forEach(m => appendMsg(
                m.content,
                m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'),
                m.time, false
            ));
        }
        lastMsgCount = msgs.length;
        scrollBottom();
    } catch { }
}

async function sendChat() {
    const input = document.getElementById('chatInput');
    const sendBtn = document.getElementById('chatSendBtn');
    const msg = input.value.trim();
    if (!msg) return;

    input.value = '';
    input.disabled = true;
    sendBtn.disabled = true;

    appendMsg(msg, 'user', now());
    showTyping();

    try {
        const res = await fetch('/Chat/SendMessage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: msg })
        });

        removeTyping();
        const data = await res.json();

        if (data.requireLogin) {
            window.location.href = '/Account/Login';
            return;
        }
        if (data.waitingForStaff) {
            appendMsg('Nhân viên đang hỗ trợ bạn, vui lòng chờ trong giây lát...', 'ai', now());
        } else if (data.reply) {
            appendMsg(data.reply, 'ai', data.time);
        }
    } catch {
        removeTyping();
        appendMsg('Có lỗi xảy ra, vui lòng thử lại.', 'ai', now());
    }

    input.disabled = false;
    sendBtn.disabled = false;
    input.focus();
}

function appendMsg(content, type, time, scroll = true) {
    const container = document.getElementById('chatMsgs');
    const div = document.createElement('div');
    div.className = `cmsg ${type}`;
    let senderHtml = '';
    if (type === 'ai') senderHtml = '<div class="cmsg-sender">🤖 AI</div>';
    if (type === 'staff') senderHtml = '<div class="cmsg-sender">👨‍💼 Nhân viên</div>';
    div.innerHTML = `
        ${senderHtml}
        <div class="cbubble">${escHtml(content)}</div>
        <div class="cmsg-time">${time}</div>
    `;
    container.appendChild(div);
    if (scroll) scrollBottom();
}

function showTyping() {
    const container = document.getElementById('chatMsgs');
    const div = document.createElement('div');
    div.id = 'typingIndicator';
    div.className = 'typing-indicator';
    div.innerHTML = '<span></span><span></span><span></span>';
    container.appendChild(div);
    scrollBottom();
}

function removeTyping() {
    document.getElementById('typingIndicator')?.remove();
}

function scrollBottom() {
    const el = document.getElementById('chatMsgs');
    if (el) el.scrollTop = el.scrollHeight;
}

function now() {
    return new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

function escHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/\n/g, '<br>');
}

// Polling tin mới mỗi 5 giây
setInterval(async () => {
    try {
        const res = await fetch('/Chat/GetMessages');
        const msgs = await res.json();

        if (!chatOpen) {
            if (msgs.length > lastMsgCount) {
                const badge = document.getElementById('fabBadge');
                badge.textContent = msgs.length - lastMsgCount;
                badge.style.display = 'flex';
            }
            return;
        }

        if (msgs.length > lastMsgCount) {
            const container = document.getElementById('chatMsgs');
            container.innerHTML = '';
            msgs.forEach(m => appendMsg(
                m.content,
                m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'),
                m.time, false
            ));
            scrollBottom();
            lastMsgCount = msgs.length;
        }
    } catch { }
}, 5000);