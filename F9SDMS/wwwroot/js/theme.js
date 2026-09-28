window.themeStorage = {
    get: function () {
        return localStorage.getItem('f9sdms-theme');
    },
    set: function (value) {
        localStorage.setItem('f9sdms-theme', value);
    }
};