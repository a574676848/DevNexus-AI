(function () {
  if (window.devnexusSearchInterop) {
    return;
  }

  function copyText(text) {
    if (!text) {
      return Promise.resolve(false);
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text).then(function () {
        return true;
      }).catch(function () {
        return false;
      });
    }

    try {
      var textarea = document.createElement("textarea");
      textarea.value = text;
      textarea.setAttribute("readonly", "");
      textarea.style.position = "absolute";
      textarea.style.left = "-9999px";
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand("copy");
      document.body.removeChild(textarea);
      return Promise.resolve(true);
    } catch (err) {
      return Promise.resolve(false);
    }
  }

  function downloadTextFile(fileName, content, mimeType) {
    if (!fileName || !content) {
      return false;
    }

    var blob = new Blob([content], { type: mimeType || "text/plain" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    return true;
  }

  window.devnexusSearchInterop = {
    copyText: copyText,
    downloadTextFile: downloadTextFile
  };
})();
