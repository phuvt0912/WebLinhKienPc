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
});