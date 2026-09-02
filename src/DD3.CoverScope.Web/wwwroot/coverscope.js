window.CoverScope = {
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
