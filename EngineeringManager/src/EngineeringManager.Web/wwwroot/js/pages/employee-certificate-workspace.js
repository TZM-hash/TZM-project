const page = document.querySelector("[data-employee-workspace]");

if (page) {
    const detailsDialog = page.querySelector("[data-certificate-details-dialog]");
    const editorDialog = page.querySelector("[data-certificate-editor-dialog]");
    const deleteDialog = page.querySelector("[data-certificate-delete-dialog]");
    const editorForm = editorDialog?.querySelector("[data-certificate-editor-form]");
    let selectedPayload = {};

    const show = (dialog) => { if (dialog && !dialog.open) dialog.showModal(); };
    const close = (dialog) => { if (dialog?.open) dialog.close(); };
    const payloadFrom = (trigger) => JSON.parse(trigger.dataset.certificatePayload || "{}");
    const detail = (name, value) => {
        const target = detailsDialog?.querySelector(`[data-certificate-detail="${name}"]`);
        if (target) target.textContent = value || "未填写";
    };
    const field = (name) => editorForm?.querySelector(`[name="Input.${name}"]`);
    const setField = (name, value) => {
        const input = field(name);
        if (!input) return;
        if (input.type === "checkbox") input.checked = Boolean(value);
        else input.value = value ?? "";
    };

    const openDetails = (payload) => {
        selectedPayload = payload;
        detail("certificateType", payload.certificateType);
        detail("employeeLabel", `${payload.employeeNumber || ""} · ${payload.employeeName || ""}`);
        ["employeeName", "employeeNumber", "certificateNumber", "specialtyLevelScope", "issuingAuthority", "issuedOn", "stateLabel", "attachmentFileName", "notes"]
            .forEach((name) => detail(name, payload[name]));
        detail("expiresOn", payload.expiresOn || "长期有效");
        show(detailsDialog);
    };

    const openEditor = (payload) => {
        if (!editorForm) return;
        selectedPayload = payload;
        editorForm.reset();
        ["Id", "EmployeeId", "CertificateType", "CertificateNumber", "SpecialtyLevelScope", "IssuingAuthority", "IssuedOn", "ExpiresOn", "ConcurrencyStamp"]
            .forEach((name) => setField(name, payload[name.charAt(0).toLowerCase() + name.slice(1)]));
        setField("ExistingAttachmentFileName", payload.attachmentFileName);
        setField("Notes", payload.notes);
        setField("Reason", "修改员工证书");
        const attachment = editorForm.querySelector("[data-certificate-existing-attachment]");
        if (attachment) attachment.textContent = `当前：${payload.attachmentFileName || "无"}`;
        const removeAttachment = editorForm.querySelector("[data-certificate-remove-attachment]");
        if (removeAttachment) removeAttachment.hidden = !payload.attachmentFileName;
        editorDialog?.querySelector("[data-certificate-editor-title]")?.replaceChildren("编辑员工证书");
        close(detailsDialog);
        show(editorDialog);
    };

    const openDelete = (payload) => {
        selectedPayload = payload;
        const form = deleteDialog?.querySelector("[data-certificate-delete-form]");
        if (form) {
            form.querySelector('[name="id"]').value = payload.id || "";
            form.querySelector('[name="concurrencyStamp"]').value = payload.concurrencyStamp || "";
        }
        const label = deleteDialog?.querySelector("[data-certificate-delete-label]");
        if (label) label.textContent = `${payload.employeeName || "员工"} · ${payload.certificateType || "证书"}`;
        show(deleteDialog);
    };

    page.querySelectorAll("[data-certificate-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            const payload = payloadFrom(trigger);
            if (trigger.dataset.certificateDialogOpen === "view") openDetails(payload);
            if (trigger.dataset.certificateDialogOpen === "edit") openEditor(payload);
            if (trigger.dataset.certificateDialogOpen === "delete") openDelete(payload);
        });
    });
    page.querySelector("[data-certificate-detail-edit]")?.addEventListener("click", () => openEditor(selectedPayload));
    page.querySelectorAll("[data-certificate-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => close(button.closest("dialog")));
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => { if (event.target === dialog) close(dialog); });
    });
}
