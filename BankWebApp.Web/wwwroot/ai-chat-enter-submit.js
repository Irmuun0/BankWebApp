(function () {
    function appendUserQuestion(form) {
        const panel = form.closest("#ai-chat-panel");
        const thread = panel?.querySelector(".codex-thread");
        const questionInput = form.querySelector("[name='Question']");
        const question = questionInput?.value?.trim();

        if (!thread || !question || thread.querySelector("[data-ai-pending-user-message]")) {
            return;
        }

        const emptyState = thread.querySelector(".codex-empty");
        if (emptyState) {
            emptyState.style.display = "none";
        }

        const wrapper = document.createElement("article");
        wrapper.className = "codex-message codex-message-user codex-pending-message";
        wrapper.setAttribute("data-ai-pending-user-message", "true");

        const label = document.createElement("div");
        label.className = "codex-message-label";
        label.textContent = "Админ";

        const bubble = document.createElement("div");
        bubble.className = "codex-bubble";
        bubble.textContent = question;

        wrapper.append(label, bubble);
        thread.appendChild(wrapper);

        questionInput.value = "";
    }

    function appendAssistantLoading(form, message) {
        const panel = form.closest("#ai-chat-panel");
        const thread = panel?.querySelector(".codex-thread");

        if (!thread || thread.querySelector("[data-ai-pending-message]")) {
            return;
        }

        const wrapper = document.createElement("article");
        wrapper.className = "codex-message codex-message-ai codex-pending-message";
        wrapper.setAttribute("data-ai-pending-message", "true");
        wrapper.innerHTML = [
            '<div class="codex-message-label">AI</div>',
            `<div class="codex-bubble codex-loading-bubble">${message}</div>`
        ].join("");

        thread.appendChild(wrapper);
        wrapper.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }

    function markSubmitLoading(form) {
        const mode = form.getAttribute("data-ai-chat-loading");

        if (mode === "chat") {
            appendUserQuestion(form);
            appendAssistantLoading(form, "Хариу бэлдэж байна...");

            const button = form.querySelector("button[type='submit']");
            if (button) {
                button.disabled = true;
            }
        }

        if (mode === "analyze") {
            const button = form.querySelector("button[type='submit']");
            if (button) {
                button.disabled = true;
                button.textContent = "Гүйлгээг шинжилж байна...";
            }
        }
    }

    document.addEventListener("keydown", function (event) {
        const target = event.target;

        if (!(target instanceof HTMLTextAreaElement || target instanceof HTMLInputElement)) {
            return;
        }

        if (!target.matches("[data-ai-chat-submit-on-enter]")) {
            return;
        }

        if (event.key !== "Enter" || event.shiftKey || event.ctrlKey || event.altKey || event.metaKey || event.isComposing) {
            return;
        }

        event.preventDefault();

        const form = target.closest("form");
        if (form) {
            form.requestSubmit();
            return;
        }

        const submitTarget = target.getAttribute("data-ai-chat-submit-target");
        const submitButton = submitTarget
            ? document.querySelector(submitTarget)
            : target.closest("[data-ai-chat-root]")?.querySelector("[data-ai-chat-submit]");

        submitButton?.click();
    });

    document.addEventListener("submit", function (event) {
        const form = event.target;

        if (!(form instanceof HTMLFormElement) || !form.matches("[data-ai-chat-loading]")) {
            return;
        }

        markSubmitLoading(form);
    }, true);
})();
