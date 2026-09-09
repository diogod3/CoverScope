(() => {
    const graphs = new WeakMap();
    const clamp = (value, low, high) => Math.min(high, Math.max(low, value));
    function detach(svg) {
        const state = graphs.get(svg);
        if (!state) return;
        state.controller.abort();
        state.observer.disconnect();
        graphs.delete(svg);
    }
    function attach(svg) {
        detach(svg);
        const controller = new AbortController();
        const state = { controller, x: 0, y: 0, scale: 1, drag: null, fitted: true };
        const size = () => svg.getBoundingClientRect();
        function render() {
            const rect = size();
            if (!rect.width || !rect.height) return;
            svg.setAttribute('viewBox', `${state.x} ${state.y} ${rect.width / state.scale} ${rect.height / state.scale}`);
        }
        function fit() {
            const rect = size();
            if (!rect.width || !rect.height) return;
            const width = Number(svg.dataset.graphWidth), height = Number(svg.dataset.graphHeight);
            state.scale = Math.min(rect.width / width, rect.height / height) * 0.94;
            state.x = (width - rect.width / state.scale) / 2;
            state.y = (height - rect.height / state.scale) / 2;
            state.fitted = true;
            render();
        }
        function zoom(factor, px, py) {
            const rect = size();
            px ??= rect.width / 2;
            py ??= rect.height / 2;
            const worldX = state.x + px / state.scale, worldY = state.y + py / state.scale;
            state.scale = clamp(state.scale * factor, 0.002, 4);
            state.x = worldX - px / state.scale;
            state.y = worldY - py / state.scale;
            state.fitted = false;
            render();
        }
        state.center = (x, y) => {
            const rect = size();
            state.scale = 1;
            state.x = x - rect.width / 2;
            state.y = y - rect.height / 2;
            state.fitted = false;
            render();
        };
        state.inspect = (panel, x, y) => {
            panel.scrollTop = 0;
            panel.scrollLeft = 0;
            if (!Number.isFinite(x) || !Number.isFinite(y)) return;
            const rect = size(), detail = panel.getBoundingClientRect();
            // Desktop details have their own column; a narrow-screen drawer can overlay
            // the canvas. Reveal the selected node without changing the user's zoom.
            const visibleWidth = detail.left < rect.right && detail.right > rect.left
                ? Math.max(0, detail.left - rect.left) : rect.width;
            if (visibleWidth < 100 || !rect.height) return;
            const width = visibleWidth / state.scale, height = rect.height / state.scale;
            const padding = 20 / state.scale;
            if (240 + 2 * padding > width) state.x = x + 120 - width / 2;
            else if (x < state.x + padding) state.x = x - padding;
            else if (x + 240 > state.x + width - padding) state.x = x + 240 - width + padding;
            if (68 + 2 * padding > height) state.y = y + 34 - height / 2;
            else if (y < state.y + padding) state.y = y - padding;
            else if (y + 68 > state.y + height - padding) state.y = y + 68 - height + padding;
            state.fitted = false;
            render();
        };
        state.transform = action => {
            if (action === 'fit') fit();
            else if (action === 'reset') zoom(1 / state.scale);
            else zoom(action === 'in' ? 1.35 : 1 / 1.35);
        };
        const listen = (name, handler, options = {}) => svg.addEventListener(name, handler, { ...options, signal: controller.signal });
        listen('wheel', event => {
            event.preventDefault();
            const rect = size();
            const delta = event.deltaY * (event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? rect.height : 1);
            zoom(Math.exp(-clamp(delta, -200, 200) * 0.004), event.clientX - rect.left, event.clientY - rect.top);
        }, { passive: false });
        listen('pointerdown', event => {
            if (event.button !== 0 || event.target.closest('[data-node], [data-edge]') || state.drag) return;
            state.drag = { id: event.pointerId, x: event.clientX, y: event.clientY };
            svg.setPointerCapture(event.pointerId);
            svg.classList.add('panning');
        });
        listen('pointermove', event => {
            if (!state.drag || state.drag.id !== event.pointerId) return;
            state.x -= (event.clientX - state.drag.x) / state.scale;
            state.y -= (event.clientY - state.drag.y) / state.scale;
            state.drag.x = event.clientX;
            state.drag.y = event.clientY;
            state.fitted = false;
            render();
        });
        const end = event => {
            if (!state.drag || event.pointerId !== state.drag.id) return;
            state.drag = null;
            svg.classList.remove('panning');
            if (svg.hasPointerCapture(event.pointerId)) svg.releasePointerCapture(event.pointerId);
        };
        listen('pointerup', end);
        listen('pointercancel', end);
        listen('lostpointercapture', end);
        listen('keydown', event => {
            if (event.target !== svg) return;
            if (event.key === '+' || event.key === '=') zoom(1.35);
            else if (event.key === '-') zoom(1 / 1.35);
            else if (event.key === '0') fit();
            else if (event.key.startsWith('Arrow')) {
                state.x += (event.key === 'ArrowRight' ? 60 : event.key === 'ArrowLeft' ? -60 : 0) / state.scale;
                state.y += (event.key === 'ArrowDown' ? 60 : event.key === 'ArrowUp' ? -60 : 0) / state.scale;
                state.fitted = false;
                render();
            } else return;
            event.preventDefault();
        });
        state.observer = new ResizeObserver(() => state.fitted ? fit() : render());
        graphs.set(svg, state);
        state.observer.observe(svg);
        const focusX = Number(svg.dataset.focusX), focusY = Number(svg.dataset.focusY);
        if (svg.dataset.focusX && svg.dataset.focusY && Number.isFinite(focusX) && Number.isFinite(focusY)) state.center(focusX, focusY);
        else fit();
    }
    window.CoverScopeReview = {
        scrollSource(element, line) {
            const rows = Array.from(element.querySelectorAll('[data-source-line]'));
            const row = rows.find(row => Number(row.dataset.sourceLine) >= Number(line)) ?? rows.at(-1);
            element.scrollTop = row ? Math.max(0, element.scrollTop + row.getBoundingClientRect().top - element.getBoundingClientRect().top - 48) : 0;
            element.scrollLeft = 0;
        },
        graph: {
            attach, detach,
            transform(svg, action) { graphs.get(svg)?.transform(action); },
            center(svg, x, y) { graphs.get(svg)?.center(x, y); },
            inspect(svg, panel, x, y) { graphs.get(svg)?.inspect(panel, x, y); }
        }
    };
})();
