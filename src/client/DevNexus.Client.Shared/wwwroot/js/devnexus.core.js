(function () {
    window.devnexus = window.devnexus || {};
    window.devnexus._scrollListeners = window.devnexus._scrollListeners || new WeakMap();
    window.devnexus._marks = window.devnexus._marks || {};

    window.scrollToBottom = function (element, force) {
        if (!element) {
            return false;
        }

        if (force) {
            element.scrollTop = element.scrollHeight;
            return true;
        }

        const threshold = 150;
        const flexBottom = element.scrollHeight - element.scrollTop - element.clientHeight;
        const isNearBottom = flexBottom < threshold;

        if (isNearBottom || flexBottom <= 0) {
            element.scrollTop = element.scrollHeight;
            return true;
        }

        return false;
    };

    window.scrollToBottomForce = function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
            return true;
        }

        return false;
    };

    window.setupScrollListener = function (element, dotNetRef) {
        if (!element || !dotNetRef) {
            return;
        }

        if (window.devnexus._scrollListeners.has(element)) {
            const oldHandler = window.devnexus._scrollListeners.get(element);
            element.removeEventListener('scroll', oldHandler);
        }

        const scrollHandler = function () {
            const threshold = 100;
            const isAtBottom = element.scrollHeight - element.scrollTop - element.clientHeight < threshold;

            try {
                dotNetRef.invokeMethodAsync('OnScrollPositionChanged', isAtBottom);
            } catch (e) {
            }
        };

        let ticking = false;
        const throttledHandler = function () {
            if (ticking) {
                return;
            }

            window.requestAnimationFrame(function () {
                scrollHandler();
                ticking = false;
            });
            ticking = true;
        };

        element.addEventListener('scroll', throttledHandler);
        window.devnexus._scrollListeners.set(element, throttledHandler);
        scrollHandler();
    };

    window.removeScrollListener = function (element) {
        if (!element) {
            return;
        }

        if (window.devnexus._scrollListeners.has(element)) {
            const handler = window.devnexus._scrollListeners.get(element);
            element.removeEventListener('scroll', handler);
            window.devnexus._scrollListeners.delete(element);
        }
    };

    window.focusElement = function (element) {
        if (element) {
            element.focus();
        }
    };

    window.devnexus.setTextareaValue = function (element, value, moveCaretToEnd) {
        if (!element) {
            return false;
        }

        const nextValue = typeof value === 'string' ? value : '';
        if (element.value !== nextValue) {
            element.value = nextValue;
        }

        if (moveCaretToEnd && typeof element.setSelectionRange === 'function') {
            const end = nextValue.length;
            element.setSelectionRange(end, end);
        }

        return true;
    };

    window.devnexus.registerSlashSkillKeydownInterceptor = function (element) {
        if (!element || element.__devnexusSlashSkillInterceptorRegistered) {
            return false;
        }

        element.addEventListener('keydown', function (event) {
            const pickerVisible = !!document.querySelector('.slash-skill-floating-panel');
            if (!pickerVisible) {
                return;
            }

            if (event.key === 'Enter' || event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Escape') {
                event.preventDefault();
            }
        });

        element.__devnexusSlashSkillInterceptorRegistered = true;
        return true;
    };

    window.devnexus.focusComposerInput = function () {
        const input = document.querySelector('.input-box textarea');
        if (!input) {
            return false;
        }

        const container = input.closest('.input-box');
        if (container && typeof container.scrollIntoView === 'function') {
            container.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }

        window.requestAnimationFrame(function () {
            input.focus();

            if (typeof input.selectionStart === 'number' && typeof input.value === 'string') {
                const end = input.value.length;
                input.setSelectionRange(end, end);
            }
        });

        return true;
    };

    window.setPointerCapture = function (element, pointerId) {
        if (element && element.setPointerCapture) {
            element.setPointerCapture(pointerId);
        }
    };

    window.releasePointerCapture = function (element, pointerId) {
        if (element && element.releasePointerCapture) {
            element.releasePointerCapture(pointerId);
        }
    };

    window.resetTextareaHeight = function (element) {
        if (element) {
            element.style.height = 'auto';
        }
    };

    window.autoResizeTextarea = function (element) {
        if (element) {
            element.style.height = 'auto';
            element.style.height = Math.min(element.scrollHeight, 200) + 'px';
        }
    };

    window.devnexus.scrollToBottom = function (element, smooth) {
        if (!element) {
            return;
        }

        if (typeof smooth === 'boolean') {
            element.scrollTo({
                top: element.scrollHeight,
                behavior: smooth ? 'smooth' : 'auto'
            });
            return;
        }

        requestAnimationFrame(function () {
            element.scrollTop = element.scrollHeight;
        });
    };

    window.devnexus.ensureActiveOptionVisible = function (container) {
        if (!container) {
            return;
        }

        requestAnimationFrame(function () {
            const activeItem = container.querySelector('.slash-skill-item.active, .slash-skill-item[aria-selected="true"]');
            if (!activeItem || typeof activeItem.scrollIntoView !== 'function') {
                return;
            }

            activeItem.scrollIntoView({
                block: 'nearest',
                inline: 'nearest',
                behavior: 'auto'
            });
        });
    };

    window.devnexus.getTextBytes = function (text) {
        if (!text) {
            return 0;
        }

        return new Blob([text]).size;
    };

    window.devnexus.formatBytes = function (bytes) {
        if (bytes < 1024) return bytes + 'B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + 'KB';
        return (bytes / (1024 * 1024)).toFixed(1) + 'MB';
    };

    window.devnexus.markStart = function (name) {
        window.devnexus._marks[name] = performance.now();
    };

    window.devnexus.markEnd = function (name) {
        if (!window.devnexus._marks[name]) {
            return 0;
        }

        const duration = performance.now() - window.devnexus._marks[name];
        delete window.devnexus._marks[name];
        return duration;
    };
})();
