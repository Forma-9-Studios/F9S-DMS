window.f9sdms = {
    // Submits the dashboard's hidden logout form (used for the automatic 8-hour logout).
    submitLogout: function () {
        const form = document.getElementById('logoutForm');
        if (form) {
            form.submit();
        }
    }
};
