let chatOpen = false;
let lastMsgCount = 0;
let welcomeSent = false;

function toggleChat() {
    chatOpen = !chatOpen;
    const box = document.getElementById('chatBox');
    box.style.display = chatOpen ? 'block' : 'none';
    document.getElementById('fabIcon').innerHTML = chatOpen ? '<i class="bi bi-x-lg fs-4"></i>' : '<i class="bi bi-chat-dots-fill fs-4"></i>';
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

        if (msgs.length === 0 && !welcomeSent) {
            // Chưa có lịch sử → AI chào trước
            welcomeSent = true;
            showTyping();
            await sendWelcomeMessage();
        } else {
            msgs.forEach(m => {
                const type = m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff');
                if (m.products && m.products.length > 0) {
                    appendMsgWithProducts(m.content, m.products, type, m.time, false);
                } else {
                    appendMsg(m.content, type, m.time, false);
                }
            });
            lastMsgCount = msgs.length;
            scrollBottom();
        }
    } catch (error) {
        console.error('Load chat history error:', error);
    }
}

async function sendWelcomeMessage() {
    try {
        const res = await fetch('/Chat/SendMessage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: '__welcome__' })
        });
        removeTyping();
        const data = await res.json();
        if (data.reply) {
            if (data.products && data.products.length > 0) {
                appendMsgWithProducts(data.reply, data.products, 'ai', now(), true);
            } else {
                appendMsg(data.reply, 'ai', now(), true);
            }
            lastMsgCount = 1;
        }
    } catch {
        removeTyping();
        appendMsg('Chào bạn! Mình là Minh từ LinhKienPC 😄 Bạn cần tư vấn gì không ạ?', 'ai', now());
    }
}

async function sendChat() {
    const input = document.getElementById('chatInput');
    const sendBtn = document.getElementById('chatSendBtn');
    const msg = input.value.trim();
    if (!msg) return;

    appendMsg(msg, 'user', now());
    input.value = '';
    input.disabled = true;
    sendBtn.disabled = true;

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
            appendMsg('Nhân viên đang online, chờ xíu nha 😄', 'ai', now());
        } else if (data.reply) {
            if (data.products && data.products.length > 0) {
                appendMsgWithProducts(data.reply, data.products, 'ai', data.time || now());
            } else {
                appendMsg(data.reply, 'ai', data.time || now());
            }
        }

        lastMsgCount++;
    } catch (error) {
        console.error('Send message error:', error);
        removeTyping();
        appendMsg('Có lỗi xảy ra, vui lòng thử lại.', 'ai', now());
    }

    input.disabled = false;
    sendBtn.disabled = false;
    input.focus();
}

// ===== RENDER SẢN PHẨM =====
function appendMsgWithProducts(content, products, type, time, scroll = true) {
    const container = document.getElementById('chatMsgs');
    const div = document.createElement('div');
    div.className = `cmsg ${type}`;

    let senderHtml = '';
    if (type === 'ai') senderHtml = '<div class="cmsg-sender"><i class="bi bi-robot me-1 text-primary"></i> AI</div>';
    if (type === 'staff') senderHtml = '<div class="cmsg-sender"><i class="bi bi-person-badge-fill me-1 text-info"></i> Nhân viên</div>';

    div.innerHTML = `
        ${senderHtml}
        <div class="cbubble has-products">
            ${escHtml(content)}
            ${renderProductCards(products)}
        </div>
        <div class="cmsg-time">${time}</div>
    `;

    container.appendChild(div);
    if (scroll) scrollBottom();
}

function renderProductCards(products) {
    if (!products || products.length === 0) return '';
    const isGrid = products.length > 3;
    return `<div class="chat-products ${isGrid ? 'grid' : ''}">
        ${products.map(p => renderSingleProduct(p, isGrid)).join('')}
    </div>`;
}

function renderSingleProduct(product, isGrid) {
    const stockClass = product.stock > 10 ? 'in-stock' : (product.stock > 0 ? 'low-stock' : 'out-stock');
    const stockText = product.stock > 0 ? `Còn ${product.stock}` : 'Hết';
    const hasImage = product.imageUrl && product.imageUrl !== '';

    return `
        <div class="product-card" onclick="window.location.href='${product.url || '#'}'">
            <div class="product-image-container ${hasImage ? '' : 'no-image'}">
                ${hasImage ? `<img src="${product.imageUrl}" class="product-image" alt="${escHtml(product.name)}">` : '<i class="bi bi-image text-muted"></i>'}
            </div>
            <div class="product-info">
                <div class="product-name">${escHtml(product.name)}</div>
                <div class="product-price-row">
                    <span class="product-price">${product.price || 'Liên hệ'}</span>
                    <span class="product-stock ${stockClass}">${stockText}</span>
                </div>
            </div>
        </div>
    `;
}

// ===== HELPERS =====
function appendMsg(content, type, time, scroll = true) {
    const container = document.getElementById('chatMsgs');
    const div = document.createElement('div');
    div.className = `cmsg ${type}`;
    let senderHtml = '';
    if (type === 'ai') senderHtml = '<div class="cmsg-sender"><i class="bi bi-robot me-1 text-primary"></i> AI</div>';
    if (type === 'staff') senderHtml = '<div class="cmsg-sender"><i class="bi bi-person-badge-fill me-1 text-info"></i> Nhân viên</div>';
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
    if (!s) return '';
    return s.replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/\n/g, '<br>');
}

// ===== POLLING =====
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
            const newMsgs = msgs.slice(lastMsgCount);
            const aiStaffMsgs = newMsgs.filter(m => !m.isFromUser);

            aiStaffMsgs.forEach(m => {
                const type = m.isFromAI ? 'ai' : 'staff';
                if (m.products && m.products.length > 0) {
                    appendMsgWithProducts(m.content, m.products, type, m.time, true);
                } else {
                    appendMsg(m.content, type, m.time, true);
                }
            });

            lastMsgCount = msgs.length;
        }
    } catch (error) {
        console.error('Polling error:', error);
    }
}, 5000);