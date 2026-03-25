let selectedUserId = null;
let selectedUserName = '';
let lastMsgCount = 0;
let lastMessageId = 0; 
let userScrolled = false;
let lastScrollTop = 0;
let isSending = false;
let isLoadingMessages = false; 

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
    userScrolled = false;
    lastScrollTop = 0;
    lastMsgCount = 0;
    lastMessageId = 0; // 🔥 FIX reset
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
    setTimeout(setupScrollListener, 500);
}

// ================= PRODUCT =================

function renderProductCards(products) {
    if (!products || products.length === 0) return '';

    if (products.length === 1) {
        return renderSingleProduct(products[0], false);
    } else if (products.length <= 3) {
        return `<div class="chat-products">
            ${products.map(p => renderSingleProduct(p, false)).join('')}
        </div>`;
    } else {
        return `<div class="chat-products grid">
            ${products.map(p => renderSingleProduct(p, true)).join('')}
        </div>`;
    }
}

function renderSingleProduct(product, isGrid) {
    const stockClass = product.stock > 10 ? 'in-stock' : (product.stock > 0 ? 'low-stock' : 'out-stock');
    const stockText = product.stock > 0 ? `Còn ${product.stock}` : 'Hết';

    const hasImage = product.imageUrl && product.imageUrl !== '/images/products/default.jpg';
    const imageContainerClass = hasImage ? 'product-image-container' : 'product-image-container no-image';

    return `
        <div class="product-card" onclick="window.location.href='${product.url || '#'}'">
            <div class="${imageContainerClass}">
                ${hasImage ? `<img src="${product.imageUrl}" class="product-image">` : ''}
            </div>
            <div class="product-info">
                <div class="product-name">${escHtml(product.name)}</div>
                <div class="product-price-row">
                    <span class="product-price">${product.price || 'Liên hệ'}</span>
                    ${!isGrid ? `<span class="product-stock ${stockClass}">${stockText}</span>` : ''}
                </div>
                ${isGrid ? `<div class="product-stock ${stockClass}">${stockText}</div>` : ''}
            </div>
        </div>
    `;
}

// ================= CHAT =================

async function loadStaffMsgs() {
    if (!selectedUserId) return;

    if (isLoadingMessages) return; // 🔥 FIX
    isLoadingMessages = true;

    try {
        const res = await fetch(`/Chat/StaffGetMessages?userId=${selectedUserId}`);
        const msgs = await res.json();
        const container = document.getElementById('scMsgs');
        if (!container) return;

        const currentScrollTop = container.scrollTop;
        const scrollHeight = container.scrollHeight;
        const clientHeight = container.clientHeight;
        const isNearBottom = scrollHeight - currentScrollTop - clientHeight < 100;

        // 🔥 lọc message mới
        const newMsgs = msgs.filter(m => m.id > lastMessageId);
        if (newMsgs.length === 0 && msgs.length !== 0) return;

        container.innerHTML = msgs.map(m => {
            const cls = m.isFromUser ? 'from-user' : (m.isFromAI ? 'from-ai' : 'from-staff');
            const label = m.isFromUser ? '👤 Khách' : (m.isFromAI ? '🤖 AI' : '👨‍💼 Bạn');

            if (m.products && m.products.length > 0) {
                return `
                    <div class="smsg ${cls}">
                        <div class="smsg-sender">${label}</div>
                        <div class="sbubble has-products">
                            ${escHtml(m.content)}
                            ${renderProductCards(m.products)}
                        </div>
                        <div class="smeta">${m.time}</div>
                    </div>
                `;
            }

            return `
                <div class="smsg ${cls}">
                    <div class="smsg-sender">${label}</div>
                    <div class="sbubble">${escHtml(m.content)}</div>
                    <div class="smeta">${m.time}</div>
                </div>
            `;
        }).join('');

        if (msgs.length > 0) {
            lastMessageId = Math.max(...msgs.map(m => m.id));
        }

        if (isNearBottom) {
            container.scrollTop = container.scrollHeight;
        }

        lastMsgCount = msgs.length;

    } catch (error) {
        console.error('Load messages error:', error);
    } finally {
        isLoadingMessages = false;
    }
}

// ================= SEND =================

async function scSend() {
    const input = document.getElementById('scInput');
    if (!input || !selectedUserId) return;

    const msg = input.value.trim();
    if (!msg) return;

    if (isSending) return;
    isSending = true;

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

        // 🔥 tránh đụng interval
        setTimeout(() => loadStaffMsgs(), 300);

    } catch (error) {
        console.error('Send error:', error);
    } finally {
        isSending = false;
    }
}

// ================= SCROLL =================

function setupScrollListener() {
    const container = document.getElementById('scMsgs');
    if (container) {
        container.removeEventListener('scroll', scrollHandler);
        container.addEventListener('scroll', scrollHandler);
    }
}

function scrollHandler() {
    const container = document.getElementById('scMsgs');
    if (!container) return;

    const isNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 100;
    userScrolled = !isNearBottom;
    lastScrollTop = container.scrollTop;
}

// ================= UTIL =================

function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

// ================= STATUS =================

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
        console.error(error);
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
        console.error(error);
    }
}

window.addEventListener('beforeunload', () => {
    navigator.sendBeacon('/Chat/SetOnlineStatus',
        new Blob([JSON.stringify(false)], { type: 'application/json' })
    );
});

// ================= INIT =================

document.addEventListener('DOMContentLoaded', () => {
    loadUsers();
    loadMyStatus();

    setInterval(loadUsers, 10000);

    setInterval(() => {
        if (selectedUserId) loadStaffMsgs();
    }, 5000);
});