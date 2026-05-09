(function () {
    window.devnexus = window.devnexus || {};

    window.devnexus.downloadFile = function (url, filename) {
        fetch(url)
            .then(function (response) { return response.blob(); })
            .then(function (blob) {
                var blobUrl = window.URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.style.display = 'none';
                a.href = blobUrl;
                a.download = filename || 'download';
                document.body.appendChild(a);
                a.click();
                window.URL.revokeObjectURL(blobUrl);
                document.body.removeChild(a);
            })
            .catch(function () { window.open(url, '_blank'); });
    };

    window.devnexus.downloadBase64File = function (fileName, base64Content, mimeType) {
        var byteCharacters = atob(base64Content);
        var byteNumbers = new Array(byteCharacters.length);

        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }

        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: mimeType || 'application/octet-stream' });
        var blobUrl = window.URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.style.display = 'none';
        a.href = blobUrl;
        a.download = fileName || 'download';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(blobUrl);
        document.body.removeChild(a);
    };

    window.devnexus.registerOpenArtifactListener = function (dotNetHelper) {
        const handler = function (event) {
            if (event.detail) {
                dotNetHelper.invokeMethodAsync(
                    'OnOpenArtifact',
                    event.detail.type || 'code',
                    event.detail.language || 'text',
                    event.detail.title || '代码',
                    event.detail.content || '');
            }
        };

        document.addEventListener('devnexus:openArtifact', handler);
        window.devnexus._openArtifactHandler = handler;
    };

    window.devnexus.removeOpenArtifactListener = function () {
        if (window.devnexus._openArtifactHandler) {
            document.removeEventListener('devnexus:openArtifact', window.devnexus._openArtifactHandler);
            window.devnexus._openArtifactHandler = null;
        }
    };

    window.highlightCode = window.highlightCode || function (element) {
        if (!element) {
            return;
        }

        if (window.devnexusMarkdown && window.devnexusMarkdown.enhanceCodeBlocks) {
            window.devnexusMarkdown.enhanceCodeBlocks(element);
            return;
        }

        const mermaidBlocks = element.querySelectorAll('pre code.language-mermaid');
        mermaidBlocks.forEach(function (code) {
            const pre = code.parentNode;
            if (pre.classList.contains('mermaid-rendered')) {
                return;
            }

            pre.classList.add('mermaid-rendered');

            const mermaidDiv = document.createElement('div');
            mermaidDiv.className = 'mermaid-container';
            mermaidDiv.innerHTML = code.textContent;

            pre.parentNode.insertBefore(mermaidDiv, pre);
            pre.style.display = 'none';

            if (window.mermaid) {
                try {
                    mermaid.init(undefined, mermaidDiv);
                } catch (e) {
                    console.warn('[DevNexus] Mermaid 渲染失败:', e);
                    pre.style.display = '';
                    mermaidDiv.remove();
                    pre.classList.remove('mermaid-rendered');
                }
            }
        });

        const preElements = element.querySelectorAll('pre:not(.enhanced):not(.mermaid-rendered):not(.chart-rendered)');
        preElements.forEach(function (pre) {
            pre.classList.add('enhanced');

            const code = pre.querySelector('code');
            let language = 'text';
            if (code) {
                const classes = code.className.split(' ');
                const langClass = classes.find(function (c) { return c.startsWith('language-'); });
                if (langClass) {
                    language = langClass.replace('language-', '');
                }
            }

            const wrapper = document.createElement('div');
            wrapper.className = 'code-block-wrapper';
            pre.parentNode.insertBefore(wrapper, pre);

            const header = document.createElement('div');
            header.className = 'code-block-header';

            const langLabel = document.createElement('span');
            langLabel.className = 'code-language';
            langLabel.textContent = language;

            const actions = document.createElement('div');
            actions.className = 'code-block-actions';

            const copyBtn = document.createElement('button');
            copyBtn.className = 'code-action-btn copy-code-btn';
            copyBtn.title = '复制代码';
            copyBtn.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                </svg>
            `;

            copyBtn.onclick = function (e) {
                e.stopPropagation();
                const text = code ? code.innerText : pre.innerText;
                navigator.clipboard.writeText(text).then(function () {
                    copyBtn.innerHTML = `
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#4ade80" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <polyline points="20 6 9 17 4 12"></polyline>
                        </svg>
                    `;
                    setTimeout(function () {
                        copyBtn.innerHTML = `
                            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                            </svg>
                        `;
                    }, 2000);
                });
            };

            const expandBtn = document.createElement('button');
            expandBtn.className = 'code-action-btn expand-code-btn';
            expandBtn.title = '在侧屏中查看';
            expandBtn.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="15 3 21 3 21 9"></polyline>
                    <polyline points="9 21 3 21 3 15"></polyline>
                    <line x1="21" y1="3" x2="14" y2="10"></line>
                    <line x1="3" y1="21" x2="10" y2="14"></line>
                </svg>
            `;

            expandBtn.onclick = function (e) {
                e.stopPropagation();

                const codeText = code ? code.innerText : pre.innerText;
                document.dispatchEvent(new CustomEvent('devnexus:openArtifact', {
                    detail: {
                        type: 'code',
                        language: language,
                        title: language.toUpperCase() + ' 代码',
                        content: codeText
                    },
                    bubbles: true,
                    cancelable: true
                }));
            };

            actions.appendChild(copyBtn);
            actions.appendChild(expandBtn);
            header.appendChild(langLabel);
            header.appendChild(actions);
            wrapper.appendChild(header);
            wrapper.appendChild(pre);

            if (window.Prism && code) {
                Prism.highlightElement(code);
            }

            const codeText = code ? code.innerText : pre.innerText;
            const lines = codeText.split('\n').length;
            const chars = codeText.length;
            const shouldAutoExpand = (
                lines >= 10 || chars >= 300 ||
                (language === 'html' && (codeText.includes('<!DOCTYPE') || codeText.includes('<html'))) ||
                (language === 'csharp' && (codeText.includes('namespace ') || codeText.includes('class '))) ||
                (language === 'java' && codeText.includes('public class ')) ||
                (language === 'python' && codeText.includes('def ') && lines >= 5) ||
                (language === 'json' && codeText.includes('"data"') && codeText.includes('"layout"'))
            );

            if (shouldAutoExpand && !pre.classList.contains('auto-expanded')) {
                pre.classList.add('auto-expanded');
                setTimeout(function () {
                    document.dispatchEvent(new CustomEvent('devnexus:openArtifact', {
                        detail: {
                            type: 'code',
                            language: language,
                            title: language.toUpperCase() + ' 代码',
                            content: codeText
                        },
                        bubbles: true,
                        cancelable: true
                    }));
                }, 100);
            }

            const isSidekickVisible = window.devnexus && window.devnexus.isSidekickVisible === true;
            if (isSidekickVisible) {
                pre.style.display = 'none';
                wrapper.classList.add('collapsed');
            }
        });

        if (window.Prism) {
            const codeBlocks = element.querySelectorAll('pre code');
            codeBlocks.forEach(function (block) {
                if (!block.classList.contains('prism-highlighted')) {
                    Prism.highlightElement(block);
                    block.classList.add('prism-highlighted');
                }
            });
        }
    };

    window.copyToClipboard = function (text) {
        if (navigator.clipboard) {
            return navigator.clipboard.writeText(text);
        }

        var textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-9999px';
        document.body.appendChild(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
        } catch (err) {
            console.error('复制失败:', err);
        }
        document.body.removeChild(textArea);
    };
})();
