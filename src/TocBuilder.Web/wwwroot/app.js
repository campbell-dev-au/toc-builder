window.tocBuilder = {
    rect(element) {
        const r = element.getBoundingClientRect();
        return [r.left, r.top, r.width, r.height];
    },
    download(fileName, content) {
        const url = URL.createObjectURL(new Blob([content], { type: 'application/json' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
    },
};
