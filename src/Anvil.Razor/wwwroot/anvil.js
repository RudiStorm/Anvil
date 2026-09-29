(() => {
    "use strict";

    let developmentInstance;
    const checkDevelopmentRefresh = async () => {
        try {
            const response = await fetch("/__anvil/reload", { cache: "no-store" });
            if (!response.ok) return;
            const body = await response.json();
            if (developmentInstance && developmentInstance !== body.instance) window.location.reload();
            developmentInstance = body.instance;
        } catch { /* Development server may not be available yet. */ }
    };
    window.setInterval(checkDevelopmentRefresh, 1000);
    checkDevelopmentRefresh();

    window.AnvilWebSocket = {
        connect(url, handlers = {}, options = {}) {
            let stopped = false;
            let attempt = 0;
            const maxDelay = options.maxDelay ?? 10000;
            const open = () => {
                if (stopped) return;
                const socket = new WebSocket(url, options.protocols);
                socket.onopen = (event) => { attempt = 0; handlers.open?.(event, socket); };
                socket.onmessage = (event) => handlers.message?.(event.data, socket);
                socket.onerror = (event) => handlers.error?.(event, socket);
                socket.onclose = (event) => {
                    handlers.close?.(event, socket);
                    if (!stopped) {
                        const delay = Math.min(maxDelay, 250 * 2 ** attempt++);
                        window.setTimeout(open, delay);
                    }
                };
                return socket;
            };
            open();
            return { stop: () => { stopped = true; } };
        }
    };

    const prefetched = new Map();
    document.addEventListener("mouseenter", (event) => {
        const link = event.target.closest("a[data-anvil-nav]");
        if (!link || !sameOrigin(link.href) || prefetched.has(link.href)) return;
        const request = fetch(link.href, { credentials: "same-origin" }).then((response) => response.text());
        prefetched.set(link.href, request);
    }, true);

    const navigate = async (url, push) => {
        try {
            const response = await fetch(url, { headers: { "X-Anvil-Navigation": "true" }, credentials: "same-origin" });
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                window.location.href = url;
                return;
            }
            const html = await response.text();
            document.open();
            document.write(html);
            document.close();
            if (push) history.pushState({ anvil: true }, "", url);
            window.scrollTo(0, 0);
        } catch {
            window.location.href = url;
        }
    };

    document.addEventListener("click", async (event) => {
        const link = event.target.closest("a[data-anvil-nav]");
        if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (!sameOrigin(link.href)) return;
        event.preventDefault();
        await navigate(link.href, true);
    });

    window.addEventListener("popstate", () => navigate(window.location.href, false));

    document.querySelectorAll("[data-anvil-island-url]").forEach((island) => {
        const url = island.dataset.anvilIslandUrl;
        if (!url || !sameOrigin(url)) return;
        const hydrate = () => fetch(url, { credentials: "same-origin" })
            .then((response) => response.ok ? response.text() : Promise.reject(response.status))
            .then((html) => { island.innerHTML = html; island.dataset.anvilHydrated = "true"; });
        if (island.dataset.anvilIslandMode === "visible" && "IntersectionObserver" in window) {
            new IntersectionObserver((entries, observer) => {
                if (entries.some((entry) => entry.isIntersecting)) { observer.disconnect(); hydrate(); }
            }).observe(island);
        } else hydrate();
    });

    const sameOrigin = (url) => {
        const parsed = new URL(url, window.location.href);
        return parsed.origin === window.location.origin;
    };

    document.addEventListener("click", async (event) => {
        const increment = event.target.closest("[data-anvil-signal-increment]");
        if (increment) {
            const name = increment.dataset.anvilSignalIncrement;
            const input = document.querySelector(`[data-anvil-signal="${CSS.escape(name)}"]`);
            if (input) {
                input.value = String(Number(input.value || 0) + 1);
                input.dispatchEvent(new Event("input", { bubbles: true }));
            }
            return;
        }

        const toggle = event.target.closest("[data-anvil-toggle]");
        if (toggle) {
            const target = document.querySelector(toggle.dataset.anvilToggle);
            if (!target) return;

            const hidden = target.toggleAttribute("hidden");
            toggle.setAttribute("aria-expanded", String(!hidden));
            return;
        }

        const partial = event.target.closest("[data-anvil-get]");
        if (!partial) return;

        const url = partial.dataset.anvilGet;
        const target = document.querySelector(partial.dataset.anvilTarget);
        if (!url || !target || !sameOrigin(url)) return;

        partial.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(url, {
                method: "POST",
                headers: { "X-Anvil-Partial": "true", "Content-Type": "application/json" },
                body: "{}",
                credentials: "same-origin"
            });
            if (!response.ok) throw new Error(`Anvil request failed: ${response.status}`);
            target.innerHTML = await response.text();
        } finally {
            partial.removeAttribute("aria-busy");
        }
    });

    document.addEventListener("input", (event) => {
        const signal = event.target.closest("[data-anvil-signal]");
        const binding = event.target.closest("[data-anvil-bind]");
        if (binding && binding.dataset.anvilBind) {
            const source = document.querySelector(`[data-anvil-signal="${CSS.escape(binding.dataset.anvilBind)}"]`);
            if (source && source !== binding) source.value = binding.value;
        }
        if (!signal && !binding) return;

        const name = signal?.dataset.anvilSignal ?? binding?.dataset.anvilBind;
        const source = document.querySelector(`[data-anvil-signal="${CSS.escape(name)}"]`);
        updateSignalBindings(name, source?.value ?? binding?.value ?? "");
    });

    document.addEventListener("submit", (event) => {
        const form = event.target.closest("form[data-anvil-validate]");
        if (!form) return;
        const invalid = [...form.querySelectorAll("[required]")].find((input) => !input.value.trim());
        if (invalid) {
            invalid.setCustomValidity("This field is required.");
            invalid.reportValidity();
            event.preventDefault();
        }
    });
    document.addEventListener("input", (event) => {
        if (event.target.matches("form[data-anvil-validate] [required]")) {
            event.target.setCustomValidity("");
        }
    });

    document.addEventListener("htmx:after:request", (event) => {
        const form = event.target.closest?.("form[data-anvil-close-modal]");
        if (!form || event.detail?.successful === false) return;
        const modal = document.querySelector(form.dataset.anvilCloseModal);
        if (modal) modal.hidden = true;
        form.reset();
    });

    document.addEventListener("submit", async (event) => {
        if (event.defaultPrevented) return;
        const form = event.target.closest("form[data-anvil-partial-form]");
        if (!form) return;

        const target = document.querySelector(form.dataset.anvilTarget);
        const action = new URL(form.getAttribute("action") || window.location.href, window.location.href);
        if (!target || !sameOrigin(action.href)) return;

        event.preventDefault();
        const method = (form.getAttribute("method") || "get").toUpperCase();
        const formData = new FormData(form);
        const options = {
            method,
            headers: { "X-Anvil-Partial": "true", "Accept": "text/html", "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        };

        if (method === "GET") {
            for (const [name, value] of formData.entries()) {
                if (typeof value === "string") action.searchParams.set(name, value);
            }
        } else {
            options.headers["Content-Type"] = "application/x-www-form-urlencoded;charset=UTF-8";
            options.body = new URLSearchParams(formData);
        }

        form.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(action.href, options);
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                throw new Error(`Anvil partial form failed: ${response.status}`);
            }
            target.innerHTML = await response.text();
            if (method === "GET" && form.dataset.anvilHistory !== "none") {
                history.replaceState({}, "", `${action.pathname}${action.search}${action.hash}`);
            }
        } catch {
            window.location.href = action.href;
        } finally {
            form.removeAttribute("aria-busy");
        }
    });

    document.querySelectorAll("[data-anvil-signal]").forEach((signal) => {
        signal.dispatchEvent(new Event("input", { bubbles: true }));
    });

    document.querySelectorAll("[data-anvil-live]").forEach((region) => {
        const url = region.dataset.anvilLive;
        const interval = Number(region.dataset.anvilLiveMs || 5000);
        let running = false;
        const update = async () => {
            if (running || !url || !sameOrigin(url)) return;
            running = true;
            try {
                const response = await fetch(url, {
                    headers: { "X-Anvil-Partial": "true" },
                    credentials: "same-origin"
                });
                if (response.ok) region.innerHTML = await response.text();
            } finally {
                running = false;
            }
        };
        window.setInterval(update, interval);
    });

    document.querySelectorAll("[data-anvil-shard]").forEach((shard) => {
        const url = shard.dataset.anvilShard;
        if (!url || !sameOrigin(url)) return;
        const signalName = shard.dataset.anvilShardSignal;
        const load = (value = null) => fetch(url, {
            method: "POST",
            headers: { "X-Anvil-Partial": "true", "Content-Type": "application/json" },
            body: value === null ? "{}" : JSON.stringify({ [signalName]: value }),
            credentials: "same-origin"
        });
        load()
          .then((response) => response.ok ? response.text() : Promise.reject(response.status))
          .then((html) => { shard.innerHTML = html; })
          .catch(() => { shard.textContent = "Unable to load this section."; });

        if (signalName) {
            document.addEventListener("input", (event) => {
                const input = event.target.closest(`[data-anvil-signal="${CSS.escape(signalName)}"]`);
                if (!input) return;
                load(input.value)
                    .then((response) => response.ok ? response.text() : Promise.reject(response.status))
                    .then((html) => { shard.innerHTML = html; });
            });
        }
    });

    function updateSignalBindings(name, value) {
        if (!name) return;
        document.querySelectorAll(`[data-anvil-signal-bind="${CSS.escape(name)}"]`)
            .forEach((target) => { target.textContent = value; });
        document.querySelectorAll(`[data-anvil-bind="${CSS.escape(name)}"]`)
            .forEach((target) => {
                if (target.value !== value) target.value = value;
            });
        document.querySelectorAll("[data-anvil-signal-attr]")
            .forEach((target) => {
                target.dataset.anvilSignalAttr.split(",").forEach((binding) => {
                    const [bindingName, attribute] = binding.split(":", 2);
                    if (bindingName !== name || !attribute) return;
                    const enabled = value === "true" || value === "1" || value === "on";
                    if (attribute === "class") target.classList.toggle("active", enabled);
                    else if (enabled) target.setAttribute(attribute, "");
                    else target.removeAttribute(attribute);
                });
            });
    }
})();
