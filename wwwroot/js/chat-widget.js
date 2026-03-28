// ===== CHAT WIDGET =====

let chatOpen = false;
let lastMessageId = 0; // Dùng ID thay vì count để tránh trùng
let welcomeSent = false;
let pollingInterval = null;
let isPolling = false;

function toggleChat() {
    chatOpen = !chatOpen;
    const box = document.getElementById('chatBox');
    box.style.display = chatOpen ? 'block' : 'none';
    document.getElementById('fabIcon').innerHTML = chatOpen ? '<i class="bi bi-x-lg fs-4"></i>' : '<i class="bi bi-chat-dots-fill fs-4"></i>';

    if (chatOpen) {
        document.getElementById('fabBadge').style.display = 'none';
        loadChatHistory();
        startPolling(); // Bắt đầu polling khi mở chat
    } else {
        stopPolling(); // Dừng polling khi đóng chat
    }
}

function startPolling() {
    if (pollingInterval) clearInterval(pollingInterval);
    pollingInterval = setInterval(pollNewMessages, 5000);
}

function stopPolling() {
    if (pollingInterval) {
        clearInterval(pollingInterval);
        pollingInterval = null;
    }
}

async function loadChatHistory() {
    try {
        const res = await fetch('/Chat/GetMessages');
        const msgs = await res.json();
        const container = document.getElementById('chatMsgs');
        container.innerHTML = '';

        if (msgs.length === 0 && !welcomeSent) {
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

            // Lấy ID tin nhắn cuối cùng
            if (msgs.length > 0) {
                lastMessageId = msgs[msgs.length - 1].id;
            }

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
            //Cập nhật ID nếu có
            if (data.messageId) lastMessageId = data.messageId;
        }
    } catch {
        removeTyping();
        appendMsg('Chào bạn! Mình là Thắng từ PTH Tech 😄 Bạn cần tư vấn gì không ạ?', 'ai', now());
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

            // Cập nhật ID tin nhắn cuối
            if (data.messageId) {
                lastMessageId = data.messageId;
            }
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

// HÀM POLL MỚI - Dùng lastMessageId để tránh trùng
async function pollNewMessages() {
    if (!chatOpen || isPolling) return;

    isPolling = true;

    try {
        //Gửi lastMessageId để server chỉ trả về tin nhắn mới
        const res = await fetch(`/Chat/GetNewMessages?lastId=${lastMessageId}`);
        const newMsgs = await res.json();

        if (newMsgs && newMsgs.length > 0) {
            // Cập nhật badge nếu chat đang đóng
            if (!chatOpen) {
                const badge = document.getElementById('fabBadge');
                badge.textContent = newMsgs.length;
                badge.style.display = 'flex';
                isPolling = false;
                return;
            }

            // Hiển thị tin nhắn mới
            newMsgs.forEach(m => {
                //CHỈ hiển thị tin nhắn từ AI và staff, KHÔNG hiển thị tin user
                if (!m.isFromUser) {
                    const type = m.isFromAI ? 'ai' : 'staff';
                    if (m.products && m.products.length > 0) {
                        appendMsgWithProducts(m.content, m.products, type, m.time, true);
                    } else {
                        appendMsg(m.content, type, m.time, true);
                    }
                }

                // ✅ Cập nhật lastMessageId
                if (m.id > lastMessageId) {
                    lastMessageId = m.id;
                }
            });
        }
    } catch (error) {
        console.error('Poll new messages error:', error);
    } finally {
        isPolling = false;
    }
}

// ===== RENDER SẢN PHẨM (giữ nguyên) =====
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