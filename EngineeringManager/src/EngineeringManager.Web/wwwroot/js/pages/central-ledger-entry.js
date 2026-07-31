const entryDialog = document.querySelector("[data-ledger-entry-dialog]");

if (entryDialog) {
    const form = entryDialog.querySelector("[data-ledger-entry-form]");
    const typeControl = entryDialog.querySelector("[data-ledger-record-type]");
    const scopeControl = entryDialog.querySelector("[data-ledger-scope]");
    const amountControl = entryDialog.querySelector("[data-ledger-amount]");
    const allocationControl = entryDialog.querySelector("[data-ledger-allocation-amount]");
    const totalPreview = entryDialog.querySelector("[data-ledger-preview-total]");
    const allocatedPreview = entryDialog.querySelector("[data-ledger-preview-allocated]");
    const remainingPreview = entryDialog.querySelector("[data-ledger-preview-remaining]");
    const readOnly = entryDialog.querySelector(".ledger-readonly-banner") !== null;

    const setVisible = (element, visible) => {
        if (!element) return;
        element.hidden = !visible;
        element.querySelectorAll("input, select, textarea").forEach(control => {
            if (control === typeControl || control === scopeControl || control === amountControl) return;
            control.disabled = !visible || readOnly;
        });
    };

    const updateFields = () => {
        const type = typeControl?.value || "Settlement";
        const scope = scopeControl?.value || "External";
        entryDialog.querySelectorAll("[data-ledger-field]").forEach(element => setVisible(element, false));
        entryDialog.querySelectorAll(`.ledger-entry-section[data-ledger-field="${type.toLowerCase()}"]`).forEach(element => setVisible(element, true));
        entryDialog.querySelectorAll(".ledger-entry-section[data-ledger-field='allocation']").forEach(element => setVisible(element, type === "Invoice" || type === "Cash"));
        entryDialog.querySelectorAll("[data-ledger-field='settlement']").forEach(element => setVisible(element, type === "Settlement"));
        entryDialog.querySelectorAll("[data-ledger-field='deduction']").forEach(element => setVisible(element, type === "Deduction"));
        entryDialog.querySelectorAll("[data-ledger-field='invoice']").forEach(element => setVisible(element, type === "Invoice"));
        entryDialog.querySelectorAll("[data-ledger-field='cash']").forEach(element => setVisible(element, type === "Cash"));
        entryDialog.querySelectorAll("[data-ledger-field='external']").forEach(element => setVisible(element, scope === "External" && type !== "Deduction"));
        entryDialog.querySelectorAll("[data-ledger-field='internal']").forEach(element => setVisible(element, scope === "Internal" && type !== "Deduction"));
        entryDialog.querySelectorAll("[data-ledger-field='project']").forEach(element => setVisible(element, type !== "Deduction"));
        entryDialog.querySelectorAll("[data-ledger-field='allocation']").forEach(element => setVisible(element, type === "Invoice" || type === "Cash"));
        entryDialog.querySelectorAll("[data-ledger-field='settlement']").forEach(element => setVisible(element, type === "Settlement"));
        if (readOnly) form?.querySelectorAll("input, select, textarea, button[type='submit']").forEach(control => { if (control.name !== "RecordId" && control.name !== "ActiveTab") control.disabled = true; });
        updatePreview();
    };

    const updatePreview = () => {
        const total = Number(amountControl?.value || 0);
        const allocated = Number(allocationControl?.value || 0);
        const remaining = Math.max(total - allocated, 0);
        const format = value => value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        if (totalPreview) totalPreview.textContent = format(total);
        if (allocatedPreview) allocatedPreview.textContent = format(allocated);
        if (remainingPreview) remainingPreview.textContent = format(remaining);
        remainingPreview?.classList.toggle("is-balanced", remaining === 0 && total > 0);
        remainingPreview?.classList.toggle("is-pending", remaining > 0);
    };

    typeControl?.addEventListener("change", updateFields);
    scopeControl?.addEventListener("change", updateFields);
    amountControl?.addEventListener("input", updatePreview);
    allocationControl?.addEventListener("input", updatePreview);
    updateFields();
}
