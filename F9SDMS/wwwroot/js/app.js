window.f9sdms = {
    // Submits the dashboard's hidden logout form (used for the automatic 8-hour logout).
    submitLogout: function () {
        const form = document.getElementById('logoutForm');
        if (form) {
            form.submit();
        }
    }
};

// ---- File download (used by "Export to Excel") ----
f9sdms.downloadFile = async function (fileName, streamRef) {
    const buffer = await streamRef.arrayBuffer();
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
};

// ---- Theme (dark / light) ----
// Saved in a cookie so the server can render the right theme on the first frame.
f9sdms.setTheme = function (theme) {
    const dark = theme === 'dark';
    document.documentElement.classList.toggle('dark-mode', dark);
    document.cookie = 'f9sdms-theme=' + (dark ? 'dark' : 'light') + '; path=/; max-age=31536000; samesite=lax';
};

f9sdms.toggleTheme = function () {
    f9sdms.setTheme(document.documentElement.classList.contains('dark-mode') ? 'light' : 'dark');
};

// One-time move of a theme saved the old way (localStorage) into the cookie.
(function () {
    try {
        if (!document.cookie.includes('f9sdms-theme=')) {
            const old = localStorage.getItem('f9sdms-theme');
            if (old === 'dark' || old === 'light') {
                f9sdms.setTheme(old);
            }
            localStorage.removeItem('f9sdms-theme');
        }
    } catch (e) { }
})();

// ---- Page loader ----
// Shown only if a navigation takes longer than a moment, so quick page changes don't flicker.
(function () {
    let timer = null;

    function loader() { return document.getElementById('page-loader'); }

    let safety = null;

    function show() {
        clearTimeout(timer);
        clearTimeout(safety);
        timer = setTimeout(function () {
            const el = loader();
            if (el) el.classList.add('visible');
        }, 250);
        // Never leave it up if a navigation is cancelled.
        safety = setTimeout(hide, 15000);
    }

    function hide() {
        clearTimeout(timer);
        const el = loader();
        if (el) el.classList.remove('visible');
    }

    f9sdms.showLoader = show;
    f9sdms.hideLoader = hide;

    // Clicking a link to another page of the app.
    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        const link = e.target.closest ? e.target.closest('a[href]') : null;
        if (!link || link.target === '_blank' || link.hasAttribute('download')) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin) return;
        if (url.pathname === location.pathname && url.search === location.search) return; // same page / #anchor
        show();
    }, true);

    // Submitting a normal form that reloads the page (login, logout). Forms handled
    // inside the page (Add Employee, etc.) cancel the submit, so they're skipped.
    document.addEventListener('submit', function (e) {
        setTimeout(function () {
            if (!e.defaultPrevented) show();
        }, 0);
    });

    // Full page loads and back/forward.
    window.addEventListener('beforeunload', show);
    window.addEventListener('pageshow', hide);

    document.addEventListener('DOMContentLoaded', function () {
        hide();
        if (window.Blazor && Blazor.addEventListener) {
            Blazor.addEventListener('enhancedload', hide);
        }
    });

    // Safety net: never leave the loader up.
    window.addEventListener('load', hide);
})();
