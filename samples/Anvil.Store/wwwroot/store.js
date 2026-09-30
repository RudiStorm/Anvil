(function () {
    const root = document.documentElement;
    const button = document.getElementById('theme-toggle');
    const saved = window.localStorage.getItem('anvil-store-theme');
    if (saved === 'light' || saved === 'dark') root.dataset.theme = saved;
    function update() {
        const light = root.dataset.theme === 'light';
        if (button) button.textContent = light ? 'Dark mode' : 'Light mode';
    }
    update();
    if (button) button.addEventListener('click', function () {
        root.dataset.theme = root.dataset.theme === 'light' ? 'dark' : 'light';
        window.localStorage.setItem('anvil-store-theme', root.dataset.theme);
        update();
    });
}());
