let chatOpen = false;
let lastMsgCount = 0;
let userId = null;

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
            msgs.forEach(m => {
                // Kiểm tra nếu tin nhắn có products (từ response mới)
                if (m.products && m.products.length > 0) {
                    appendMsgWithProducts(m.content, m.products, m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'), m.time, false);
                } else {
                    appendMsg(m.content, m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'), m.time, false);
                }
            });
        }
        lastMsgCount = msgs.length;
        scrollBottom();
    } catch (error) {
        console.error('Load chat history error:', error);
    }
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
            // Kiểm tra nếu có kèm sản phẩm
            if (data.products && data.products.length > 0) {
                appendMsgWithProducts(data.reply, data.products, 'ai', data.time || now());
            } else {
                appendMsg(data.reply, 'ai', data.time || now());
            }

            // Cập nhật lastMsgCount để tránh load lại
            lastMsgCount++;
        }
    } catch (error) {
        console.error('Send message error:', error);
        removeTyping();
        appendMsg('Có lỗi xảy ra, vui lòng thử lại.', 'ai', now());
    }

    input.disabled = false;
    sendBtn.disabled = false;
    input.focus();
}

// Hàm append tin nhắn kèm card sản phẩm
function appendMsgWithProducts(content, products, type, time, scroll = true) {
    const container = document.getElementById('chatMsgs');
    const div = document.createElement('div');
    div.className = `cmsg ${type}`;
    div.setAttribute('data-has-products', 'true'); // Đánh dấu tin nhắn có sản phẩm

    let senderHtml = '';
    if (type === 'ai') senderHtml = '<div class="cmsg-sender">🤖 AI</div>';
    if (type === 'staff') senderHtml = '<div class="cmsg-sender">👨‍💼 Nhân viên</div>';

    // Tạo HTML cho card sản phẩm
    const productsHtml = renderProductCards(products);

    div.innerHTML = `
        ${senderHtml}
        <div class="cbubble has-products">
            ${escHtml(content)}
            ${productsHtml}
        </div>
        <div class="cmsg-time">${time}</div>
    `;

    container.appendChild(div);
    if (scroll) scrollBottom();
}

// Hàm render card sản phẩm
function renderProductCards(products) {
    if (!products || products.length === 0) return '';

    if (products.length === 1) {
        return renderSingleProduct(products[0], false);
    } else if (products.length <= 3) {
        return `
            <div class="chat-products">
                ${products.map(p => renderSingleProduct(p, false)).join('')}
            </div>
        `;
    } else {
        return `
            <div class="chat-products grid">
                ${products.map(p => renderSingleProduct(p, true)).join('')}
            </div>
        `;
    }
}

// Hàm render 1 sản phẩm
function renderSingleProduct(product, isGrid) {
    const stockClass = product.stock > 10 ? 'in-stock' : (product.stock > 0 ? 'low-stock' : 'out-stock');
    const stockText = product.stock > 0 ? `Còn ${product.stock} cái` : 'Hết hàng';

    return `
        <div class="product-card" onclick="window.location.href='${product.url || '#'}'">
            <img src="${product.imageUrl || '/images/products/default.jpg'}" 
                 class="product-image" 
                 alt="${product.name}"
                 onerror="this.src='/images/products/default.jpg'">
            <div class="product-info">
                <div class="product-name">${escHtml(product.name)}</div>
                <div class="product-price">${product.price || 'Liên hệ'}</div>
                ${!isGrid ? `<div class="product-stock ${stockClass}">${stockText}</div>` : ''}
            </div>
        </div>
    `;
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
    if (!s) return '';
    return s.replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/\n/g, '<br>');
}

// Polling tin mới mỗi 5 giây - CHỈ CẬP NHẬT SỐ LƯỢNG, KHÔNG LOAD LẠI TOÀN BỘ
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

        // CHỈ cập nhật nếu có tin nhắn mới
        if (msgs.length > lastMsgCount) {
            // Lấy tin nhắn mới nhất
            const newMsgs = msgs.slice(lastMsgCount);

            newMsgs.forEach(m => {
                if (m.products && m.products.length > 0) {
                    appendMsgWithProducts(m.content, m.products, m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'), m.time, true);
                } else {
                    appendMsg(m.content, m.isFromUser ? 'user' : (m.isFromAI ? 'ai' : 'staff'), m.time, true);
                }
            });

            lastMsgCount = msgs.length;
        }
    } catch (error) {
        console.error('Polling error:', error);
    }
}, 5000);