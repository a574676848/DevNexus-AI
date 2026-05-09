(function () {
    window.devnexusSilkyMarkdown = window.devnexusSilkyMarkdown || {};

    if (window.devnexusSilkyMarkdown._shared) {
        return;
    }

    window.devnexusSilkyMarkdown._shared = {
        instances: new Map(),
        md: null
    };
})();
