let currentFormId = null;

function confirmDelete(id, name) {
    currentFormId = id;
    document.getElementById('modalName').textContent = name;
    document.getElementById('modalOverlay').classList.add('show');
}

function closeModal() {
    document.getElementById('modalOverlay').classList.remove('show');
    currentFormId = null;
}

function submitDelete() {
    if (currentFormId) {
        document.getElementById('form-' + currentFormId).submit();
    }
}

document.addEventListener('keydown', e => {
    if (e.key === 'Escape') closeModal();
})
function filterOrders(val) {
    const keyword = val.trim().toLowerCase();
    const rows = document.querySelectorAll('.ord-table tbody tr:not(.no-result-row)');
    const btnClear = document.getElementById('btnClear');

    btnClear.style.display = val ? 'block' : 'none';

    let visibleCount = 0;

    rows.forEach(row => {
        const code = row.querySelector('.ord-id')?.textContent.toLowerCase() ?? '';
        const show = !keyword || code.includes(keyword);
        row.style.display = show ? '' : 'none';
        if (show) visibleCount++;
    });

    // Hiện/ẩn dòng "không tìm thấy"
    let noResult = document.querySelector('.no-result-row');
    if (visibleCount === 0) {
        if (!noResult) {
            noResult = document.createElement('tr');
            noResult.className = 'no-result-row';
            noResult.innerHTML = `<td colspan="5">
                <div class="empty-state">
                    <span class="empty-icon">🔍</span>
                    Không tìm thấy đơn hàng <strong>"${val}"</strong>
                </div>
            </td>`;
            document.querySelector('.ord-table tbody').appendChild(noResult);
        }
        noResult.style.display = '';
    } else if (noResult) {
        noResult.style.display = 'none';
    }
}

function clearSearch() {
    const input = document.getElementById('searchInput');
    input.value = '';
    filterOrders('');
    input.focus();
};