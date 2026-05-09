(function () {
    'use strict';

    window.devnexusSilkyMarkdown = window.devnexusSilkyMarkdown || {};
    var shared = window.devnexusSilkyMarkdown._shared;

    function init(containerId) {
        var container = document.getElementById(containerId);
        if (!container) {
            console.warn('[SilkyMarkdown] 容器不存在:', containerId);
            return false;
        }

        shared.instances.set(containerId, {
            container: container,
            lastContent: '',
            lastHtml: '',
            pendingEnhance: false
        });

        window.devnexusSilkyMarkdown.initMarkdownIt();
        return true;
    }

    function render(containerId, content, isStreaming) {
        var instance = shared.instances.get(containerId);
        if (!instance) {
            console.warn('[SilkyMarkdown] 实例不存在:', containerId);
            return;
        }

        if (content === instance.lastContent) {
            return;
        }

        instance.lastContent = content;
        var processedContent = isStreaming
            ? window.devnexusSilkyMarkdown.preprocessStreaming(content)
            : content;

        processedContent = window.devnexusSilkyMarkdown.preprocessMarkdown(processedContent);

        var parser = window.devnexusSilkyMarkdown.initMarkdownIt();
        var html;
        if (parser) {
            try {
                html = parser.render(processedContent);
            } catch (e) {
                console.error('[SilkyMarkdown] 解析失败:', e);
                html = '<pre style="white-space:pre-wrap;">' + window.devnexusSilkyMarkdown.escapeHtml(content) + '</pre>';
            }
        } else {
            html = '<pre style="white-space:pre-wrap;">' + window.devnexusSilkyMarkdown.escapeHtml(content) + '</pre>';
        }

        window.devnexusSilkyMarkdown.patchDOM(instance.container, html);
        instance.lastHtml = html;
        instance.pendingEnhance = true;
    }

    function enhance(containerId) {
        var instance = shared.instances.get(containerId);
        if (!instance) {
            return;
        }

        instance.pendingEnhance = false;
        window.devnexusSilkyMarkdown.enhanceCodeBlocks(instance.container);
    }

    function dispose(containerId) {
        shared.instances.delete(containerId);
    }

    window.devnexusSilkyMarkdown.init = init;
    window.devnexusSilkyMarkdown.render = render;
    window.devnexusSilkyMarkdown.enhance = enhance;
    window.devnexusSilkyMarkdown.dispose = dispose;

    window.devnexusMarkdown = {
        init: window.devnexusSilkyMarkdown.initMarkdownIt,
        render: function (markdown) {
            var parser = window.devnexusSilkyMarkdown.initMarkdownIt();
            if (!parser) {
                return '<pre style="white-space:pre-wrap;">' + window.devnexusSilkyMarkdown.escapeHtml(markdown) + '</pre>';
            }

            return parser.render(window.devnexusSilkyMarkdown.preprocessMarkdown(markdown));
        },
        enhanceCodeBlocks: window.devnexusSilkyMarkdown.enhanceCodeBlocks
    };

    window.devnexus = window.devnexus || {};
    window.devnexus.registerOpenArtifactListener = function (dotNetHelper) {
        if (!dotNetHelper) {
            return;
        }

        document.addEventListener('devnexus:openArtifact', function (e) {
            var detail = e.detail;
            try {
                dotNetHelper.invokeMethodAsync('OnOpenArtifact', detail.type, detail.language, detail.title, detail.content);
            } catch (err) {
                console.error('[SilkyMarkdown] 调用 .NET 方法失败:', err);
            }
        });
    };

    window.devnexus.syncSidekickState = window.devnexusSilkyMarkdown.syncSidekickState;
    console.log('[SilkyMarkdown] 丝滑 Markdown 渲染模块已加载');
})();
