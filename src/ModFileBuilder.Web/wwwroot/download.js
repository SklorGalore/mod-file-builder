window.modBuilder = {
    getDarkMode() {
        return document.documentElement.dataset.theme === 'dark';
    },
    setDarkMode(enabled) {
        document.documentElement.dataset.theme = enabled ? 'dark' : 'light';
        try {
            localStorage.setItem('mod-file-builder-theme', enabled ? 'dark' : 'light');
        } catch { }
    },
    download(name, content) {
        const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }
};
