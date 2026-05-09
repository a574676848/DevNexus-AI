/**
 * DevNexus Drag-and-Drop and Paste Handler
 * Handles file drag and drop events and clipboard paste events for the InputBox.
 */

window.DevNexusDragDrop = {
    /**
     * Initialize drag and drop listeners
     * @param {HTMLElement} element - The element to attach listeners to
     * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
     */
    initDragDrop: function (element, dotNetRef) {
        if (!element) return;

        // Prevent default behavior (Prevent file from being opened)
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            element.addEventListener(eventName, preventDefaults, false);
        });

        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        // Highlight notification
        ['dragenter', 'dragover'].forEach(eventName => {
            element.addEventListener(eventName, highlight, false);
        });

        ['dragleave', 'drop'].forEach(eventName => {
            element.addEventListener(eventName, unhighlight, false);
        });

        function highlight(e) {
            element.classList.add('drag-active');
        }

        function unhighlight(e) {
            element.classList.remove('drag-active');
        }

        // Handle dropped files
        element.addEventListener('drop', handleDrop, false);

        function handleDrop(e) {
            var dt = e.dataTransfer;
            var files = dt.files;

            if (files.length > 0) {
                handleFiles(files);
            }
        }

        function handleFiles(files) {
            // Process each file
            Array.from(files).forEach(file => {
                uploadFile(file);
            });
        }

        function uploadFile(file) {
            // Read file as ArrayBuffer to send to Blazor
            var reader = new FileReader();
            reader.onloadend = function () {
                var arrayBuffer = reader.result;
                var bytes = new Uint8Array(arrayBuffer);
                
                // Send to Blazor
                dotNetRef.invokeMethodAsync('HandleFileDrop', {
                    name: file.name,
                    type: file.type || 'application/octet-stream',
                    size: file.size,
                    data: bytes
                });
            };
            reader.readAsArrayBuffer(file);
        }
    },

    /**
     * Initialize paste listener
     * @param {HTMLElement} element - The element (usually textarea or container)
     * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
     */
    initPasteHandler: function (element, dotNetRef) {
        if (!element) return;

        element.addEventListener('paste', handlePaste);

        function handlePaste(e) {
            var items = (e.clipboardData || e.originalEvent.clipboardData).items;
            
            for (var index in items) {
                var item = items[index];
                if (item.kind === 'file') {
                    var blob = item.getAsFile();
                    if (blob) {
                        e.preventDefault(); // Prevent default paste behavior if it's a file
                        
                        var reader = new FileReader();
                        reader.onload = function (event) {
                            var base64 = event.target.result.split(',')[1]; // Remove data URL prefix
                            
                            dotNetRef.invokeMethodAsync('HandlePasteImage', {
                                name: "pasted_image_" + new Date().getTime() + ".png", // Generate a name
                                type: blob.type,
                                size: blob.size,
                                base64Data: base64
                            });
                        };
                        reader.readAsDataURL(blob);
                        return; // Handle only the first file for now or continue?
                    }
                }
            }
        }
    }
};
