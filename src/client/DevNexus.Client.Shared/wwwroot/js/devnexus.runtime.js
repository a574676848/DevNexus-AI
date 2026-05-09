(function () {
    window.devnexus = window.devnexus || {};

    window.typewriter = {
        queue: [],
        isRunning: false,

        append: function (element, text, speed) {
            if (!element || !text) {
                return;
            }

            speed = speed || 10;
            this.queue.push({ element: element, text: text, speed: speed });

            if (!this.isRunning) {
                this.processQueue();
            }
        },

        processQueue: function () {
            if (this.queue.length === 0) {
                this.isRunning = false;
                return;
            }

            this.isRunning = true;
            var item = this.queue.shift();
            var element = item.element;
            var text = item.text;
            var speed = item.speed;
            var index = 0;
            var self = this;

            function type() {
                if (index >= text.length) {
                    self.processQueue();
                    return;
                }

                element.innerHTML += text[index];
                index++;
                element.scrollTop = element.scrollHeight;

                if (index < text.length) {
                    setTimeout(type, speed);
                } else {
                    self.processQueue();
                }
            }

            type();
        },

        clear: function () {
            this.queue = [];
            this.isRunning = false;
        }
    };

    window.storage = {
        get: function (key) {
            try {
                var value = localStorage.getItem(key);
                return value ? JSON.parse(value) : null;
            } catch (e) {
                return localStorage.getItem(key);
            }
        },

        set: function (key, value) {
            try {
                localStorage.setItem(key, JSON.stringify(value));
            } catch (e) {
                localStorage.setItem(key, value);
            }
        },

        remove: function (key) {
            localStorage.removeItem(key);
        }
    };

    window.devnexus.updateIframeContent = function (iframe, content) {
        if (!iframe || !content) {
            return;
        }

        try {
            var doc = iframe.contentDocument || iframe.contentWindow.document;
            doc.open();
            doc.write(content);
            doc.close();
        } catch (e) {
            console.error('[DevNexus] 更新 iframe 内容失败:', e);
        }
    };

    window.devnexus.highlightCodeBlocks = function (container) {
        if (!container || !window.Prism) {
            return;
        }

        var codeBlocks = container.querySelectorAll('pre code:not(.prism-highlighted)');
        codeBlocks.forEach(function (block) {
            Prism.highlightElement(block);
            block.classList.add('prism-highlighted');
        });
    };

    window.devnexus.terminal = window.devnexus.terminal || {};
    window.devnexus.terminal.autoScroll = function (element) {
        if (!element) {
            return;
        }

        var threshold = 100;
        var isAtBottom = element.scrollHeight - element.scrollTop - element.clientHeight < threshold;
        if (isAtBottom) {
            element.scrollTop = element.scrollHeight;
        }
    };

    window.devnexus.terminal.appendLine = function (container, content, isError) {
        if (!container || !content) {
            return;
        }

        var line = document.createElement('div');
        line.className = 'console-line' + (isError ? ' error' : '');

        var timestamp = document.createElement('span');
        timestamp.className = 'timestamp';
        timestamp.textContent = new Date().toLocaleTimeString('zh-CN', { hour12: false });

        var text = document.createElement('span');
        text.className = 'content';
        text.textContent = content;

        line.appendChild(timestamp);
        line.appendChild(text);
        container.appendChild(line);

        window.devnexus.terminal.autoScroll(container);
    };

    window.devnexus.charts = window.devnexus.charts || { _instances: {} };
    window.devnexus.charts.renderChart = function (canvasId, data, type) {
        var ctx = document.getElementById(canvasId);
        if (!ctx) {
            return;
        }

        if (this._instances[canvasId]) {
            this._instances[canvasId].destroy();
        }

        this._instances[canvasId] = new Chart(ctx, {
            type: type || 'line',
            data: data,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            color: getComputedStyle(document.body).getPropertyValue('--text-secondary') || '#94a3b8'
                        }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: getComputedStyle(document.body).getPropertyValue('--border-subtle') || 'rgba(255, 255, 255, 0.1)'
                        },
                        ticks: {
                            color: getComputedStyle(document.body).getPropertyValue('--text-muted') || '#64748b'
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: getComputedStyle(document.body).getPropertyValue('--text-muted') || '#64748b'
                        }
                    }
                }
            }
        });
    };

    window.devnexus.charts.destroyChart = function (canvasId) {
        if (this._instances[canvasId]) {
            this._instances[canvasId].destroy();
            delete this._instances[canvasId];
        }
    };

    window.getFileInfo = function (inputElement) {
        if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
            return null;
        }

        var file = inputElement.files[0];
        return {
            name: file.name,
            type: file.type,
            size: file.size
        };
    };

    window.getFileAsBase64 = function (inputElement) {
        return new Promise(function (resolve, reject) {
            if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
                resolve(null);
                return;
            }

            var file = inputElement.files[0];
            var reader = new FileReader();

            reader.onload = function (e) {
                var result = e.target.result;
                resolve(result.split(',')[1]);
            };

            reader.onerror = function () {
                reject(new Error('读取文件失败'));
            };

            reader.readAsDataURL(file);
        });
    };

    window.enableDraggable = function (elmSelector, handleSelector) {
        const elm = document.querySelector(elmSelector);
        const handle = document.querySelector(handleSelector);
        if (!elm || !handle) {
            return;
        }

        if (handle._dragMouseDown) {
            handle.removeEventListener('mousedown', handle._dragMouseDown);
        }

        let isDragging = false;
        let startX = 0;
        let startY = 0;
        let initialLeft = 0;
        let initialTop = 0;

        let overlay = document.getElementById('drag-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'drag-overlay';
            overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:9999;cursor:move;display:none;';
            document.body.appendChild(overlay);
        }

        handle._dragMouseDown = function (e) {
            e.preventDefault();
            isDragging = true;

            const style = window.getComputedStyle(elm);
            initialLeft = parseInt(style.left, 10) || 0;
            initialTop = parseInt(style.top, 10) || 0;
            startX = e.clientX;
            startY = e.clientY;

            overlay.style.display = 'block';
            document.addEventListener('mousemove', elementDrag);
            document.addEventListener('mouseup', closeDragElement);
        };

        handle.addEventListener('mousedown', handle._dragMouseDown);

        function elementDrag(e) {
            if (!isDragging) {
                return;
            }

            e.preventDefault();
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;

            requestAnimationFrame(function () {
                elm.style.left = (initialLeft + dx) + 'px';
                elm.style.top = (initialTop + dy) + 'px';
            });
        }

        function closeDragElement() {
            isDragging = false;
            overlay.style.display = 'none';
            document.removeEventListener('mousemove', elementDrag);
            document.removeEventListener('mouseup', closeDragElement);
        }
    };

    window.triggerFileInput = function (inputElement) {
        if (inputElement) {
            inputElement.click();
        }
    };

    window.calculateContextMenuPosition = function (mouseX, mouseY, menuWidth, menuHeight) {
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;
        const padding = 8;

        let x = mouseX;
        let y = mouseY;

        if (x + menuWidth + padding > viewportWidth) {
            x = Math.max(padding, viewportWidth - menuWidth - padding);
        }

        if (x < padding) {
            x = padding;
        }

        if (y + menuHeight + padding > viewportHeight) {
            y = Math.max(padding, mouseY - menuHeight);
        }

        if (y < padding) {
            y = padding;
        }

        return { x: x, y: y };
    };
})();
