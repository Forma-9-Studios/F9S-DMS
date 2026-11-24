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

// ---- Password rules on the change / set password pages: tick them off while typing ----
(function () {
    function updateRules(list) {
        var input = document.getElementById(list.dataset.new);
        if (!input) return;
        var v = input.value || '';
        var current = list.dataset.current ? document.getElementById(list.dataset.current) : null;
        var checks = {
            length: v.length >= 8,
            cases: /[a-z]/.test(v) && /[A-Z]/.test(v),
            digit: /[0-9]/.test(v),
            different: v.length > 0 && (!current || v !== current.value)
        };
        list.querySelectorAll('li[data-rule]').forEach(function (li) {
            li.classList.toggle('met', !!checks[li.dataset.rule]);
        });
    }
    document.addEventListener('input', function (e) {
        if (!e.target || !e.target.id) return;
        document.querySelectorAll('.pw-rules').forEach(function (list) {
            if (e.target.id === list.dataset.new || e.target.id === list.dataset.current) updateRules(list);
        });
    });

    // Close the account menu when clicking anywhere else.
    document.addEventListener('click', function (e) {
        document.querySelectorAll('details.avatar-menu[open]').forEach(function (menu) {
            if (!menu.contains(e.target)) menu.removeAttribute('open');
        });
    });
})();

// ---- Phones: close the slide-in menu after picking a page ----
document.addEventListener('click', function (e) {
    var link = e.target && e.target.closest ? e.target.closest('.sidebar a, .sidebar button') : null;
    var toggle = document.getElementById('navToggle');
    if (link && toggle) toggle.checked = false;
});

// ---- Show / hide password (sign-in and change-password pages) ----
f9sdms.togglePassword = function (button, inputId) {
    var input = document.getElementById(inputId);
    if (!input) return;
    var show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    button.classList.toggle('on', show);
    button.setAttribute('aria-pressed', show ? 'true' : 'false');
};

// ---- Close the account menu (e.g. when opening the profile photo dialog) ----
f9sdms.closeAvatarMenus = function () {
    document.querySelectorAll('details.avatar-menu[open]').forEach(function (m) { m.removeAttribute('open'); });
};

// ---- Profile photo cropper: pick a photo, drag and zoom it in a circle, export a small JPEG ----
f9sdms.photo = (function () {
    var st = null;

    function clamp() {
        var size = st.canvas.width, s = st.base * st.zoom;
        var w = st.img.naturalWidth * s, h = st.img.naturalHeight * s;
        st.x = Math.min(0, Math.max(size - w, st.x));
        st.y = Math.min(0, Math.max(size - h, st.y));
    }

    function draw() {
        var ctx = st.canvas.getContext('2d');
        ctx.clearRect(0, 0, st.canvas.width, st.canvas.height);
        if (!st.img) return;
        clamp();
        var s = st.base * st.zoom;
        ctx.drawImage(st.img, st.x, st.y, st.img.naturalWidth * s, st.img.naturalHeight * s);
    }

    function load(src) {
        var img = new Image();
        img.onload = function () {
            var size = st.canvas.width;
            st.img = img;
            st.base = Math.max(size / img.naturalWidth, size / img.naturalHeight);
            st.zoom = 1;
            if (st.zoomEl) st.zoomEl.value = 1;
            st.x = (size - img.naturalWidth * st.base) / 2;
            st.y = (size - img.naturalHeight * st.base) / 2;
            st.canvas.classList.add('has-image');
            draw();
        };
        img.src = src;
    }

    return {
        init: function (canvasId, fileId, zoomId, existingUrl) {
            var canvas = document.getElementById(canvasId);
            if (!canvas) return;
            st = { canvas: canvas, zoomEl: document.getElementById(zoomId), img: null, x: 0, y: 0, zoom: 1, base: 1 };

            var file = document.getElementById(fileId);
            if (file) file.addEventListener('change', function () {
                var f = file.files && file.files[0];
                if (!f) return;
                var reader = new FileReader();
                reader.onload = function () { load(reader.result); };
                reader.readAsDataURL(f);
            });

            if (st.zoomEl) st.zoomEl.addEventListener('input', function () {
                if (!st.img) return;
                var size = st.canvas.width, c = size / 2;
                var before = st.base * st.zoom, z = parseFloat(st.zoomEl.value) || 1, after = st.base * z;
                // Zoom around the centre of the circle.
                st.x = c - (c - st.x) * after / before;
                st.y = c - (c - st.y) * after / before;
                st.zoom = z;
                draw();
            });

            var drag = null;
            canvas.addEventListener('pointerdown', function (e) {
                if (!st.img) return;
                drag = { x: e.clientX, y: e.clientY, ox: st.x, oy: st.y };
                canvas.setPointerCapture(e.pointerId);
            });
            canvas.addEventListener('pointermove', function (e) {
                if (!drag) return;
                var k = canvas.width / canvas.getBoundingClientRect().width;
                st.x = drag.ox + (e.clientX - drag.x) * k;
                st.y = drag.oy + (e.clientY - drag.y) * k;
                draw();
            });
            ['pointerup', 'pointercancel'].forEach(function (name) {
                canvas.addEventListener(name, function () { drag = null; });
            });

            if (existingUrl) load(existingUrl);
        },

        // Returns the cropped photo as base64 JPEG (no "data:" prefix), or null if none was chosen.
        export: function (size) {
            if (!st || !st.img) return null;
            var out = document.createElement('canvas');
            out.width = out.height = size;
            var ctx = out.getContext('2d');
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, size, size);
            var k = size / st.canvas.width, s = st.base * st.zoom * k;
            ctx.drawImage(st.img, st.x * k, st.y * k, st.img.naturalWidth * s, st.img.naturalHeight * s);
            var data = out.toDataURL('image/jpeg', 0.88);
            return data.slice(data.indexOf(',') + 1);
        }
    };
})();

// ---- Lost connection: reload automatically ----
// Blazor marks the reconnect box "rejected" when the server restarted (the page can't be
// resumed) and "failed" when it gave up retrying. Either way, wait until the server answers
// again and reload, so an open tab never sits on "Connection lost" and gets clocked out.
f9sdms.watchReconnect = function () {
    const modal = document.getElementById('components-reconnect-modal');
    if (!modal) return;
    let reloading = false;

    const reloadWhenServerIsBack = async function () {
        if (reloading) return;
        reloading = true;
        for (;;) {
            try {
                const response = await fetch('/', { cache: 'no-store', credentials: 'same-origin' });
                if (response.ok) break;
            } catch (e) {
                // Still offline.
            }
            await new Promise(function (resolve) { setTimeout(resolve, 5000); });
        }
        location.reload();
    };

    new MutationObserver(function () {
        const c = modal.classList;
        if (c.contains('components-reconnect-rejected') || c.contains('components-reconnect-failed')) {
            reloadWhenServerIsBack();
        }
    }).observe(modal, { attributes: true, attributeFilter: ['class'] });
};
