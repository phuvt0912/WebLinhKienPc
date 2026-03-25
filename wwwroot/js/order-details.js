// order-details.js

// Modal functions
function showCancelModal() {
    document.getElementById('modalOverlay').classList.add('show');
}

function closeModal() {
    document.getElementById('modalOverlay').classList.remove('show');
}

function submitCancelDetail() {
    document.getElementById('cancelForm').submit();
}

// Update order status via AJAX
function updateOrderStatus(orderId, newStatus) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    fetch('/AdminOrder/UpdateStatusAjax', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify({ orderId: orderId, status: newStatus })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('success', data.message);
                setTimeout(() => location.reload(), 1500);
            } else {
                showToast('error', data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi cập nhật trạng thái');
        });
}

// Show toast notification
function showToast(type, message) {
    // Remove existing toast (except server toast)
    const existingToast = document.querySelector('.od-toast');
    if (existingToast && existingToast.id !== 'toast') {
        existingToast.remove();
    }

    const toast = document.createElement('div');
    toast.className = `od-toast ${type}`;
    const icon = type === 'success' ? '✅' : '❌';
    toast.innerHTML = `${icon} ${message}`;

    const wrap = document.querySelector('.od-wrap');
    const header = document.querySelector('.od-header');
    wrap.insertBefore(toast, header);

    // Auto hide after 3 seconds
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.4s';
        setTimeout(() => {
            if (toast.parentNode) toast.remove();
        }, 400);
    }, 3000);
}

// Auto hide server toast
function initServerToast() {
    const toast = document.getElementById('toast');
    if (toast) {
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.4s';
            setTimeout(() => {
                if (toast.parentNode) toast.remove();
            }, 400);
        }, 3000);
    }
}

// Event listeners
function initEventListeners() {
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeModal();
    });
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initServerToast();
    initEventListeners();
});