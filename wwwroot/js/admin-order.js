let pendingFormId = null;
let selectedOrders = new Set();

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

// Kiểm tra đơn hàng có thể chọn không (không phải Completed hoặc Cancelled)
function isSelectable(row) {
    const status = row.getAttribute('data-status');
    return status !== 'status-completed' && status !== 'status-cancelled';
}

// Toggle chọn tất cả (CHỈ chọn các đơn có thể chọn)
function toggleSelectAll(checkbox) {
    const allRows = document.querySelectorAll('#ordBody tr[data-order-id]');
    // Chỉ lấy các row có thể chọn và đang hiển thị (không bị filter ẩn)
    const selectableRows = Array.from(allRows).filter(row => {
        return isSelectable(row) && row.style.display !== 'none';
    });

    selectableRows.forEach(row => {
        const orderId = row.getAttribute('data-order-id');
        const cb = row.querySelector('.check-col input[type="checkbox"]');
        if (cb) {
            cb.checked = checkbox.checked;
            if (checkbox.checked) {
                selectedOrders.add(orderId);
            } else {
                selectedOrders.delete(orderId);
            }
        }
    });

    // Cập nhật checkbox header
    const selectAllHeader = document.getElementById('selectAllHeader');
    if (selectAllHeader && selectAllHeader !== checkbox) {
        selectAllHeader.checked = checkbox.checked;
    }

    updateBulkUpdateButton();
}

function toggleOrderSelection(orderId, checkbox) {
    if (checkbox.checked) {
        selectedOrders.add(orderId.toString());
    } else {
        selectedOrders.delete(orderId.toString());
        // Bỏ chọn checkbox chính
        const selectAllCheckbox = document.getElementById('selectAll');
        const selectAllHeader = document.getElementById('selectAllHeader');
        if (selectAllCheckbox) selectAllCheckbox.checked = false;
        if (selectAllHeader) selectAllHeader.checked = false;
    }
    updateBulkUpdateButton();
}

function updateBulkUpdateButton() {
    const btn = document.getElementById('bulkUpdateBtn');
    const count = selectedOrders.size;
    if (btn) {
        btn.textContent = `Cập nhật (${count})`;
        btn.disabled = count === 0;
    }
}

function saveOldStatus(selectElement) {
    selectElement.setAttribute('data-old-value', selectElement.value);
}

function updateOrderStatus(orderId, selectElement) {
    const newStatus = selectElement.value;
    if (!newStatus) return;

    const oldValue = selectElement.getAttribute('data-old-value');
    selectElement.disabled = true;

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
                showToast('success', data.message);
                setTimeout(() => location.reload(), 1500);
            } else {
                showToast('error', data.message);
                selectElement.value = oldValue;
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi cập nhật trạng thái');
            selectElement.value = oldValue;
        })
        .finally(() => {
            selectElement.disabled = false;
        });
}

function showToast(type, message) {
    // Xóa toast cũ
    const oldToast = document.querySelector('.toast:not(#toast)');
    if (oldToast) oldToast.remove();

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    const icon = type === 'success' ? '✅' : '❌';
    toast.innerHTML = `${icon} ${message}`;
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.4s';
        setTimeout(() => {
            if (toast.parentNode) toast.remove();
        }, 400);
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

function bulkUpdateStatus() {
    const newStatus = document.getElementById('bulkStatus').value;
    if (!newStatus) {
        showToast('error', 'Vui lòng chọn trạng thái cần cập nhật');
        return;
    }

    if (selectedOrders.size === 0) {
        showToast('error', 'Vui lòng chọn đơn hàng cần cập nhật');
        return;
    }

    const btn = document.getElementById('bulkUpdateBtn');
    const originalText = btn.textContent;
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
                showToast('success', data.message);
                selectedOrders.clear();
                setTimeout(() => location.reload(), 1500);
            } else {
                showToast('error', data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi cập nhật hàng loạt');
        })
        .finally(() => {
            btn.textContent = originalText;
            btn.disabled = false;
        });
}

// Auto hide toast from server
function initToast() {
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

// Initialize event listeners
function initEventListeners() {
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeModal();
    });
}

// Initialize everything when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initToast();
    initEventListeners();

    // Disable checkboxes cho đơn đã hoàn thành hoặc đã hủy
    const rows = document.querySelectorAll('#ordBody tr[data-order-id]');
    rows.forEach(row => {
        const checkbox = row.querySelector('.check-col input[type="checkbox"]');
        if (checkbox && !isSelectable(row)) {
            checkbox.disabled = true;
            checkbox.style.opacity = '0.5';
            checkbox.style.cursor = 'not-allowed';
        }
    });

    // Cập nhật số lượng selected ban đầu (nếu có)
    updateBulkUpdateButton();
});