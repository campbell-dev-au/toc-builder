window.tocBuilder = {
    devicePixelRatio() { return window.devicePixelRatio; },
    rect(element) {
        const r = element.getBoundingClientRect();
        return [r.left, r.top, r.width, r.height];
    },
    download(fileName, content, type = 'application/json') {
        tocBuilder.save(fileName, new Blob([content], { type }));
    },
    save(fileName, blob) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
    },
    // Draws the SVG on a white canvas at twice its size, within the largest canvas browsers allow.
    async downloadPng(fileName, svg) {
        const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
        try {
            const image = new Image();
            image.src = url;
            await image.decode();
            const scale = Math.min(2, 8192 / Math.max(image.width, image.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(image.width * scale);
            canvas.height = Math.round(image.height * scale);
            const context = canvas.getContext('2d');
            context.fillStyle = '#fff';
            context.fillRect(0, 0, canvas.width, canvas.height);
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
            if (!blob) throw new Error('the browser could not make the image');
            tocBuilder.save(fileName, blob);
        } finally {
            URL.revokeObjectURL(url);
        }
    },
};
