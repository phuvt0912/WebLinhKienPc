let pendingFormId = null;

function confirmCancel(id, name, code) {
    pendingFormId = id;
    document.getElementById('modalName').textContent = `#${code} - ${name}`;
    document.getElementById('modalOverlay').classList.add('show');
}

function closeModal() {
    document.getElementById('modalOverlay').classList.remove('show');
    pendingFormId = null;
}

function submitCancel() {
    if (pendingFormId) {
        document.getElementById('form-' + pendingFormId).submit();
    }
}

function applyFilter() {
    const keyword = document.getElementById('searchInput').value.toLowerCase().trim();
    const status = document.getElementById('statusFilter').value;
    const rows = document.querySelectorAll('#ordBody tr[data-code]');
    const btnClear = document.getElementById('btnClear');

    btnClear.style.display = keyword ? 'block' : 'none';

    rows.forEach(row => {
        const matchCode = !keyword || row.dataset.code?.includes(keyword);
        const matchStatus = !status || row.dataset.status === status;
        row.style.display = matchCode && matchStatus ? '' : 'none';
    });
}

function clearSearch() {
    document.getElementById('searchInput').value = '';
    applyFilter();
}

// Auto hide toast after 3 seconds
function initToast() {
    const toast = document.getElementById('toast');
    if (toast) {
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.4s';
            setTimeout(() => toast.remove(), 400);
        }, 3000);
    }
}

// Initialize event listeners
function initEventListeners() {
    // Escape key to close modal
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeModal();
    });
}

// Initialize everything when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initToast();
    initEventListeners();
});

// Thêm vào cuối file AdminOrder.js
let selectedOrders = new Set();

function updateOrderStatus(orderId, selectElement) {
    const newStatus = selectElement.value;
    if (!newStatus) return;

    // Hiển thị loading
    const btn = selectElement.nextElementSibling;
    const originalText = btn.textContent;
    btn.textContent = '⏳ Đang cập nhật...';
    btn.disabled = true;

    fetch('/AdminOrder/UpdateStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ orderId: orderId, status: newStatus })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('success', `✅ ${data.message}`);
                setTimeout(() => location.reload(), 1000);
            } else {
                showToast('error', `❌ ${data.message}`);
                // Reset dropdown về giá trị cũ
                selectElement.value = selectElement.getAttribute('data-old-value');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', '❌ Có lỗi xảy ra');
            selectElement.value = selectElement.getAttribute('data-old-value');
        })
        .finally(() => {
            btn.textContent = originalText;
            btn.disabled = false;
        });
}

function saveOldStatus(selectElement) {
    selectElement.setAttribute('data-old-value', selectElement.value);
}

function showToast(type, message) {
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = message;
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.4s';
        setTimeout(() => toast.remove(), 400);
    }, 3000);
}

function filterByDate() {
    const fromDate = document.getElementById('fromDate').value;
    const toDate = document.getElementById('toDate').value;

    if (!fromDate && !toDate) {
        applyFilter();
        return;
    }

    const rows = document.querySelectorAll('#ordBody tr[data-date]');

    rows.forEach(row => {
        const orderDate = row.dataset.date;
        let show = true;

        if (fromDate && orderDate < fromDate) show = false;
        if (toDate && orderDate > toDate) show = false;

        row.style.display = show ? '' : 'none';
    });
}

function resetDateFilter() {
    document.getElementById('fromDate').value = '';
    document.getElementById('toDate').value = '';
    applyFilter();
}

function exportToExcel() {
    window.location.href = '/AdminOrder/ExportExcel';
}

function toggleSelectAll(checkbox) {
    const allCheckboxes = document.querySelectorAll('#ordBody input[type="checkbox"]');
    allCheckboxes.forEach(cb => {
        cb.checked = checkbox.checked;
        if (checkbox.checked) {
            selectedOrders.add(cb.value);
        } else {
            selectedOrders.delete(cb.value);
        }
    });
    updateBulkUpdateButton();
}

function toggleOrderSelection(orderId, checkbox) {
    if (checkbox.checked) {
        selectedOrders.add(orderId);
    } else {
        selectedOrders.delete(orderId);
        document.getElementById('selectAll').checked = false;
    }
    updateBulkUpdateButton();
}

function updateBulkUpdateButton() {
    const btn = document.getElementById('bulkUpdateBtn');
    const count = selectedOrders.size;
    btn.textContent = `Cập nhật (${count})`;
    btn.disabled = count === 0;
}

function bulkUpdateStatus() {
    const newStatus = document.getElementById('bulkStatus').value;
    if (!newStatus) {
        showToast('error', '❌ Vui lòng chọn trạng thái cần cập nhật');
        return;
    }

    if (selectedOrders.size === 0) {
        showToast('error', '❌ Vui lòng chọn đơn hàng cần cập nhật');
        return;
    }

    if (confirm(`Bạn có chắc muốn cập nhật ${selectedOrders.size} đơn hàng sang trạng thái ${newStatus}?`)) {
        const btn = document.getElementById('bulkUpdateBtn');
        btn.textContent = '⏳ Đang xử lý...';
        btn.disabled = true;

        fetch('/AdminOrder/BulkUpdateStatus', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                orderIds: Array.from(selectedOrders),
                status: newStatus
            })
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    showToast('success', `✅ ${data.message}`);
                    setTimeout(() => location.reload(), 1500);
                } else {
                    showToast('error', `❌ ${data.message}`);
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showToast('error', '❌ Có lỗi xảy ra');
            })
            .finally(() => {
                btn.textContent = `Cập nhật (${selectedOrders.size})`;
                btn.disabled = false;
            });
    }
}