(function () {
    function initDirtyForms() {
        document.querySelectorAll('form[data-dirty-check="true"]').forEach((form) => {
            const submit = form.querySelector('button[type="submit"]');
            if (!submit) {
                return;
            }

            const fields = Array.from(form.querySelectorAll("input, select, textarea"))
                .filter((field) => field.name && field.type !== "hidden");
            const initial = new Map(fields.map((field) => [field.name, field.value || ""]));
            const sync = () => {
                submit.disabled = !fields.some((field) => (field.value || "") !== (initial.get(field.name) || ""));
            };

            fields.forEach((field) => {
                field.removeEventListener("input", sync);
                field.removeEventListener("change", sync);
                field.addEventListener("input", sync);
                field.addEventListener("change", sync);
            });

            sync();
        });
    }

    document.addEventListener("DOMContentLoaded", initDirtyForms);
    document.addEventListener("enhancedload", initDirtyForms);
    initDirtyForms();
})();
