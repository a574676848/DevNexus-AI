(function () {
    'use strict';

    window.devnexusSilkyMarkdown = window.devnexusSilkyMarkdown || {};
    var shared = window.devnexusSilkyMarkdown._shared;

    function escapeHtml(str) {
        if (!str) {
            return '';
        }

        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function initMarkdownIt() {
        if (shared.md) {
            return shared.md;
        }

        if (!window.markdownit) {
            console.error('[SilkyMarkdown] markdown-it 未加载');
            return null;
        }

        shared.md = window.markdownit({
            html: true,
            xhtmlOut: false,
            breaks: true,
            langPrefix: 'language-',
            linkify: true,
            typographer: true,
            highlight: function (str, lang) {
                var langClass = lang ? 'language-' + lang : 'language-text';
                return '<pre><code class="' + langClass + '">' + escapeHtml(str) + '</code></pre>';
            }
        });

        var defaultLinkRender = shared.md.renderer.rules.link_open || function (tokens, idx, options, env, self) {
            return self.renderToken(tokens, idx, options);
        };

        shared.md.renderer.rules.link_open = function (tokens, idx, options, env, self) {
            var token = tokens[idx];
            var hrefIndex = token.attrIndex('href');

            if (hrefIndex >= 0) {
                var href = token.attrs[hrefIndex][1];
                if (href.startsWith('http://') || href.startsWith('https://')) {
                    token.attrPush(['target', '_blank']);
                    token.attrPush(['rel', 'noopener noreferrer']);
                }
            }

            return defaultLinkRender(tokens, idx, options, env, self);
        };

        return shared.md;
    }

    function preprocessStreaming(content) {
        if (!content) {
            return '';
        }

        var result = content;
        var codeBlockMatches = result.match(/```/g) || [];
        if (codeBlockMatches.length % 2 !== 0) {
            result += '\n```';
        }

        var boldMatches = result.match(/\*\*/g) || [];
        if (boldMatches.length % 2 !== 0) {
            result += '**';
        }

        var allStars = (result.match(/\*/g) || []).length;
        var doubleStars = (result.match(/\*\*/g) || []).length * 2;
        var singleStars = allStars - doubleStars;
        if (singleStars % 2 !== 0) {
            result += '*';
        }

        var allBackticks = (result.match(/`/g) || []).length;
        var tripleBackticks = (result.match(/```/g) || []).length * 3;
        var singleBackticks = allBackticks - tripleBackticks;
        if (singleBackticks % 2 !== 0) {
            result += '`';
        }

        return result;
    }

    function preprocessMarkdown(markdown) {
        return markdown || '';
    }

    function canUseStreamingTextFastPath(content) {
        if (!content) {
            return true;
        }

        return !/[#*_`\[\]<>\|!]/.test(content)
            && !/(^|\n)\s*(?:[-+]\s|\d+\.\s|>\s)/.test(content)
            && !/https?:\/\//i.test(content);
    }

    function renderStreamingTextFastPath(instance, content) {
        if (!instance || !instance.container) {
            return false;
        }

        if (instance.lastRenderMode !== 'streamText') {
            instance.container.textContent = '';
            var textNode = document.createElement('div');
            textNode.className = 'silky-markdown-stream-text';
            textNode.appendChild(document.createTextNode(content || ''));
            instance.container.appendChild(textNode);
            instance.streamingTextNode = textNode;
            instance.lastStreamingTextContent = content || '';
            instance.lastRenderMode = 'streamText';
            return true;
        }

        var node = instance.streamingTextNode;
        if (!node || !instance.container.contains(node)) {
            instance.lastRenderMode = '';
            return renderStreamingTextFastPath(instance, content);
        }

        var nextContent = content || '';
        var previousContent = instance.lastStreamingTextContent || '';
        if (nextContent.startsWith(previousContent) && node.firstChild) {
            node.firstChild.appendData(nextContent.slice(previousContent.length));
        } else {
            node.textContent = nextContent;
        }

        instance.lastStreamingTextContent = nextContent;
        return true;
    }

    function patchDOM(container, newHtml) {
        if (!container) {
            return;
        }

        var temp = document.createElement('div');
        temp.innerHTML = newHtml;

        if (window.Idiomorph) {
            try {
                Idiomorph.morph(container, temp, {
                    morphStyle: 'innerHTML',
                    ignoreActive: true,
                    callbacks: {
                        beforeNodeMorphed: function (oldNode, newNode) {
                            if (oldNode.nodeType === 1 && oldNode.classList) {
                                if (oldNode.classList.contains('prism-highlighted') && oldNode.textContent === newNode.textContent) {
                                    return false;
                                }
                            }

                            return true;
                        }
                    }
                });
                return;
            } catch (e) {
                console.warn('[SilkyMarkdown] idiomorph 失败，降级为 innerHTML:', e);
            }
        }

        container.innerHTML = newHtml;
    }

    window.devnexusSilkyMarkdown.escapeHtml = escapeHtml;
    window.devnexusSilkyMarkdown.initMarkdownIt = initMarkdownIt;
    window.devnexusSilkyMarkdown.preprocessStreaming = preprocessStreaming;
    window.devnexusSilkyMarkdown.preprocessMarkdown = preprocessMarkdown;
    window.devnexusSilkyMarkdown.canUseStreamingTextFastPath = canUseStreamingTextFastPath;
    window.devnexusSilkyMarkdown.renderStreamingTextFastPath = renderStreamingTextFastPath;
    window.devnexusSilkyMarkdown.patchDOM = patchDOM;
})();
