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
            .then(function () { showStatus('离线外壳可用'); })
            .catch(function () { showStatus('在线模式'); });
    } else {
        showStatus('在线模式');
    }
}());
