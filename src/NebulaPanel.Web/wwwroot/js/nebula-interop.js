window.nebulaInterop = {
    downloadFile: function (dataUrl, fileName) {
        var a = document.createElement('a');
        a.href = dataUrl;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },
    focusElement: function (selector) {
        var el = document.querySelector(selector);
        if (el) el.focus();
    }
};
