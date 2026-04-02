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
                setTimeout(() => location.reload(), 500);
            } else {
                showAdminToast('error', data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAdminToast('error', 'Có lỗi xảy ra khi cập nhật trạng thái');
        });
}


// Event listeners
function initEventListeners() {
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeModal();
    });
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initEventListeners();
});