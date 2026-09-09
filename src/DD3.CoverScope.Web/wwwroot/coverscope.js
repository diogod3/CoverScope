window.CoverScope = {
    openDialog: function (dialog) {
        if (!dialog.open) dialog.showModal();
    },
    closeDialog: function (dialog) {
        if (dialog.open) dialog.close();
    },
    download: function (filename, text) {
        const url = URL.createObjectURL(new Blob([text], {type: filename.endsWith('.json') ? 'application/json' : 'text/markdown'}));
        const link = document.createElement('a');
        link.href = url; link.download = filename; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    scrollToLine: function (id) {
        window.requestAnimationFrame(function () {
            const element = document.getElementById(id);
            if (element) element.scrollIntoView({ behavior: "smooth", block: "center" });
        });
    },
    copyText: async function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return;
        }

        const input = document.createElement("textarea");
        input.value = text;
        input.style.position = "fixed";
        input.style.opacity = "0";
        document.body.appendChild(input);
        input.select();
        document.execCommand("copy");
        input.remove();
    }
};
