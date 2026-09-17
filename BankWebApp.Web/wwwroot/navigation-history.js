(function () {
    const storageKey = "phoebe.navigation.stack";
    const maxItems = 10;

    function currentUrl() {
        return window.location.pathname + window.location.search;
    }

    function readStack() {
        try {
            const parsed = JSON.parse(sessionStorage.getItem(storageKey) || "[]");
            return Array.isArray(parsed) ? parsed.filter((item) => typeof item === "string") : [];
        } catch {
            return [];
        }
    }

    function writeStack(stack) {
        sessionStorage.setItem(storageKey, JSON.stringify(stack.slice(-maxItems)));
    }

    function pushCurrent() {
        const stack = readStack();
        const current = currentUrl();
        if (stack[stack.length - 1] !== current) {
            stack.push(current);
            writeStack(stack);
        }
    }

    function handleBack(event) {
        const link = event.target.closest("[data-app-back]");
        if (!link) {
            return;
        }

        const current = currentUrl();
        const stack = readStack();
        while (stack.length > 0 && stack[stack.length - 1] === current) {
            stack.pop();
        }

        const previous = stack.pop();
        if (!previous) {
            return;
        }

        event.preventDefault();
        writeStack([...stack, previous]);
        window.location.assign(previous);
    }

    document.addEventListener("click", handleBack);
    document.addEventListener("DOMContentLoaded", pushCurrent);
    document.addEventListener("enhancedload", pushCurrent);
    pushCurrent();
})();
