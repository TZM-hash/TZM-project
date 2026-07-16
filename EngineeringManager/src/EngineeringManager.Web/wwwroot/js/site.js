(function () {
    const menuToggle = document.querySelector('[data-menu-toggle]');
    const navigation = document.querySelector('[data-navigation]');
    if (menuToggle && navigation) {
        menuToggle.addEventListener('click', function () {
            const isOpen = navigation.classList.toggle('is-open');
            menuToggle.setAttribute('aria-expanded', String(isOpen));
        });
    }

    const badge = document.querySelector('[data-pwa-badge]');
    const status = document.querySelector('[data-pwa-status]');
    const showStatus = function (message) {
        if (badge) badge.textContent = message;
        if (status) status.textContent = message;
    };

    if ('serviceWorker' in navigator && (window.isSecureContext || window.location.hostname === 'localhost')) {
        navigator.serviceWorker.register('/service-worker.js')
            .then(function (registration) {
                showStatus('离线外壳可用');
                if (registration.waiting) showStatus('发现新版本，请刷新页面');
                registration.addEventListener('updatefound', function () {
                    const worker = registration.installing;
                    worker?.addEventListener('statechange', function () {
                        if (worker.state === 'installed' && navigator.serviceWorker.controller) showStatus('发现新版本，请刷新页面');
                    });
                });
            })
            .catch(function () { showStatus('在线模式'); });
    } else {
        showStatus('在线模式');
    }

    const offlineDashboard = document.querySelector('[data-dashboard-offline]');
    if (offlineDashboard) {
        const counts = JSON.parse(localStorage.getItem('engineering-manager-offline-counts:' + offlineDashboard.dataset.userId) || '{"pending":0,"failed":0,"conflicts":0}');
        offlineDashboard.querySelector('[data-dashboard-pending]').textContent = counts.pending || 0;
        offlineDashboard.querySelector('[data-dashboard-failed]').textContent = counts.failed || 0;
        offlineDashboard.querySelector('[data-dashboard-conflicts]').textContent = counts.conflicts || 0;
    }
}());
