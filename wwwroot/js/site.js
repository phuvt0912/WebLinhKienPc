// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', function () {
    // Initialize Swipers only if Swiper is loaded
    if (typeof Swiper !== 'undefined') {
        var swiper = new Swiper(".mySwiper", {
            slidesPerView: "auto",
            spaceBetween: 20,
        });

        var hotSwiper = new Swiper(".hotSwiper", {
            slidesPerView: 2,
            spaceBetween: 16,
            loop: true,
            autoplay: {
                delay: 3000,
                disableOnInteraction: false,
            },
            navigation: {
                nextEl: ".hot-next",
                prevEl: ".hot-prev",
            },
            breakpoints: {
                576: { slidesPerView: 3, spaceBetween: 15 },
                768: { slidesPerView: 4, spaceBetween: 20 },
                992: { slidesPerView: 5, spaceBetween: 20 },
                1200: { slidesPerView: 5, spaceBetween: 20 }
            }
        });

        var bannerSwiper = new Swiper(".bannerSwiper", {
            loop: true,
            autoplay: {
                delay: 3000,
            },
            pagination: {
                el: ".swiper-pagination",
                clickable: true,
            },
        });
    }

    // === QUICK VIEW: Event Delegation (đọc dữ liệu từ data-attributes của thẻ cha .product-card) ===
    document.body.addEventListener('click', function (e) {
        const btn = e.target.closest('.btn-qv-trigger');
        if (!btn) return;
        const card = btn.closest('.product-card');
        if (!card) return;

        const id       = parseInt(card.dataset.id);
        const name     = card.dataset.name;
        const img      = card.dataset.img;
        const price    = parseFloat(card.dataset.price);
        const category = card.dataset.category;
        const stock    = parseInt(card.dataset.stock);
        const desc     = card.dataset.desc;

        openQuickView(id, name, img, price, category, stock, desc);
    });
});

// ===== QUICK VIEW =====
let currentProductId = 0;
let currentStock = 0;

function openQuickView(id, name, img, price, category, stock, desc) {
    currentProductId = id;
    currentStock = stock;

    const imgEl = document.getElementById('qvImg');
    const imgWrap = document.getElementById('qvImgWrap');

    if (img) {
        imgWrap.innerHTML = '';
        const newImg = document.createElement('img');
        newImg.id = 'qvImg';
        newImg.className = 'qv-img';
        newImg.src = img;
        imgWrap.appendChild(newImg);
    } else {
        imgWrap.innerHTML = '<div class="qv-img-empty">📷</div>';
    }

    document.getElementById('qvName').textContent = name;
    document.getElementById('qvPrice').textContent = Number(price).toLocaleString('vi-VN') + ' ₫';
    document.getElementById('qvCategory').textContent = category || 'Chưa phân loại';
    document.getElementById('qvBuyProductId').value = id;
    document.getElementById('qvBuyProductQty').value = 1;
    document.getElementById('qvBtnDetail').href = '/Product/Details/' + id;

    const color = stock > 10 ? '#4cdf8a' : stock > 0 ? '#ffaa00' : '#ff4f7b';
    const label = stock > 10 ? 'Còn hàng' : stock > 0 ? `Còn ${stock} sản phẩm` : 'Hết hàng';
    document.getElementById('qvStock').innerHTML = `
        <span class="qv-stock-dot" style="background:${color}"></span>
        <span style="color:${color}">${label}</span>
    `;

    const qtyInput = document.getElementById('qvQuantity');
    qtyInput.value = 1;
    qtyInput.max = stock;

    const disabled = stock <= 0;
    document.getElementById('qvAddToCartBtn').disabled = disabled;
    document.getElementById('qvBuyNowBtn').disabled = disabled;
    document.getElementById('qvBtnIncrease').disabled = disabled;
    document.getElementById('qvBtnDecrease').disabled = disabled;
    qtyInput.disabled = disabled;

    const descEl = document.getElementById('qvDesc');
    if (desc && desc.trim()) {
        const lines = desc.split(/\r?\n|\\n/).filter(x => x.trim());
        if (lines.length > 0) {
            descEl.innerHTML = lines.map(line => `
                <div class="qv-desc-item">
                    <span class="qv-desc-bullet">•</span>
                    <span class="qv-desc-text">${line.trim()}</span>
                </div>`).join('');
        } else {
            descEl.innerHTML = '<div class="qv-desc-empty">Không có mô tả chi tiết</div>';
        }
    } else {
        descEl.innerHTML = '<div class="qv-desc-empty">Không có mô tả chi tiết</div>';
    }

    document.getElementById('qvOverlay').classList.add('show');
    document.body.style.overflow = 'hidden';
}

function qvIncrease() {
    const input = document.getElementById('qvQuantity');
    const val = parseInt(input.value);
    if (val < currentStock) {
        input.value = val + 1;
        document.getElementById('qvBuyProductQty').value = input.value;
    }
}

function qvDecrease() {
    const input = document.getElementById('qvQuantity');
    const val = parseInt(input.value);
    if (val > 1) {
        input.value = val - 1;
        document.getElementById('qvBuyProductQty').value = input.value;
    }
}

document.addEventListener('DOMContentLoaded', function () {
    const qtyInput = document.getElementById('qvQuantity');
    if (qtyInput) {
        qtyInput.addEventListener('input', function () {
            let v = parseInt(this.value) || 1;
            if (v > currentStock) v = currentStock;
            if (v < 1) v = 1;
            this.value = v;
            document.getElementById('qvBuyProductQty').value = v;
        });
    }
});

// ===== AJAX ADD TO CART =====
async function qvAddToCart() {
    const productId = currentProductId;
    const quantity = parseInt(document.getElementById('qvQuantity').value) || 1;
    const btn = document.getElementById('qvAddToCartBtn');
    const tokenEl = document.querySelector('#qvBuyForm input[name="__RequestVerificationToken"]');

    if (!tokenEl) {
        window.location.href = '/Account/Login';
        return;
    }

    const token = tokenEl.value;
    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-hourglass-split me-1 fs-5"></i> Đang thêm...';

    try {
        const res = await fetch('/Cart/AddToCart', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': token
            },
            body: `productId=${productId}&quantity=${quantity}`
        });

        if (res.redirected || res.url.includes('/Account/Login')) {
            window.location.href = '/Account/Login';
            return;
        }

        const data = await res.json();

        if (data.requireLogin) {
            window.location.href = '/Account/Login';
            return;
        }

        if (data.success) {
            showToast('Thêm vào giỏ thành công!', `Đã thêm ${quantity} sản phẩm vào giỏ hàng.`, 'success');
            const badge = document.getElementById('cartBadge');
            if (badge && data.cartCount !== undefined) {
                badge.textContent = data.cartCount;
                badge.style.display = 'flex';
            }
        } else if (data.requireLogin) {
            window.location.href = '/Account/Login';
        } else {
            showToast('Không thể thêm vào giỏ', data.message || 'Vui lòng thử lại.', 'error');
        }
    } catch (e) {
        window.location.href = '/Account/Login';
    }

    setTimeout(() => {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-cart-plus me-1 fs-5"></i> Thêm giỏ hàng';
    }, 1000);
}

// ===== TOAST =====
function showToast(title, msg, type = 'success') {
    const wrap = document.getElementById('toastWrap');
    if (!wrap) return;
    const toast = document.createElement('div');
    toast.className = 'toast-item';

    const isSuccess = type === 'success';
    const borderColor = isSuccess ? 'rgba(76,223,138,0.3)' : 'rgba(255,79,123,0.3)';
    const iconHtml = isSuccess 
        ? '<i class="bi bi-check-circle-fill" style="color:#4cdf8a"></i>' 
        : '<i class="bi bi-x-circle-fill" style="color:#ff4f7b"></i>';
    toast.style.borderColor = borderColor;

    toast.innerHTML = `
        <span class="toast-icon">${iconHtml}</span>
        <div class="toast-content">
            <div class="toast-title ${isSuccess ? '' : 'error'}">${title}</div>
            <div class="toast-msg">${msg}</div>
        </div>
        <button class="toast-close" onclick="this.parentElement.remove()">✕</button>
        <div class="toast-progress ${isSuccess ? '' : 'error'}"></div>
    `;

    wrap.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('hiding');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function closeQuickView() {
    const overlay = document.getElementById('qvOverlay');
    if (overlay) overlay.classList.remove('show');
    document.body.style.overflow = '';
}

document.addEventListener('keydown', e => {
    if (e.key === 'Escape') closeQuickView();
});
