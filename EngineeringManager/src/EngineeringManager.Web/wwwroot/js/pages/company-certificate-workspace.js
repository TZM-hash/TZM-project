import { initAttachmentPreview } from "../components/attachment-preview.js";

const page = document.querySelector("[data-company-certificate-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-company-certificate-editor-dialog]");
    const detailsDialog = page.querySelector("[data-company-certificate-details-dialog]");
    const deleteDialog = page.querySelector("[data-company-certificate-delete-dialog]");
    const editorForm = editorDialog?.querySelector("[data-company-certificate-editor-form]");

    const show = (dialog) => {
        if (dialog && !dialog.open) dialog.showModal();
    };
    const payloadFrom = (trigger) => JSON.parse(trigger.dataset.companyCertificatePayload || "{}");
    const field = (name) => editorForm?.querySelector(`[name="Editor.${name}"]`);
    const setField = (name, value) => {
        const input = field(name);
        if (!input) return;
        if (input.type === "checkbox") input.checked = Boolean(value);
        else input.value = value ?? "";
    };
    const setDetail = (selector, value) => {
        const target = detailsDialog?.querySelector(selector);
        if (target) target.textContent = value || "未填写";
    };

    const form = page.querySelector(".workbench-inline-filters");
    if (form) {
        const companySelect = form.querySelector('[name="CompanyId"]');
        companySelect?.addEventListener("change", () => form.requestSubmit());
    }

    const showCurrentAttachment = (payload) => {
        const shell = editorForm?.querySelector("[data-company-certificate-current-attachment]");
        const name = editorForm?.querySelector("[data-company-certificate-attachment-name]");
        if (!shell || !name) return;
        shell.hidden = !payload.attachmentFileName;
        name.textContent = payload.attachmentFileName || "";
    };

    const openEditor = (mode, payload = {}) => {
        editorForm?.reset();
        const copy = mode === "copy";
        const editing = mode === "edit";
        setField("Id", editing ? payload.id : "");
        setField("ConcurrencyStamp", editing ? payload.concurrencyStamp : "");
        setField("LegalEntityId", payload.legalEntityId || page.dataset.companyId || "");
        setField("CertificateType", payload.certificateType);
        setField("CertificateNumber", copy ? "" : payload.certificateNumber);
        setField("SpecialtyLevelScope", payload.specialtyLevelScope);
        setField("IssuingAuthority", payload.issuingAuthority);
        setField("IssuedOn", copy ? "" : payload.issuedOn);
        setField("ExpiresOn", copy ? "" : payload.expiresOn);
        setField("ExistingAttachmentFileName", editing ? payload.attachmentFileName : "");
        setField("RemoveAttachment", false);
        setField("Notes", copy ? "" : payload.notes);
        setField("Reason", copy ? "复制公司证书" : editing ? "修改公司证书" : "新增公司证书");
        const title = editorDialog?.querySelector("[data-company-certificate-editor-title]");
        if (title) title.textContent = copy ? "复制公司证书" : editing ? "编辑公司证书" : "新增公司证书";
        const deleteButton = editorDialog?.querySelector("[data-company-certificate-delete-open]");
        if (deleteButton) {
            deleteButton.hidden = !editing;
            deleteButton.dataset.companyCertificatePayload = editing ? JSON.stringify(payload) : "";
        }
        showCurrentAttachment(editing ? payload : {});
        show(editorDialog);
    };

    const openDetails = (payload) => {
        setDetail("[data-company-certificate-detail-title]", payload.certificateType);
        setDetail("[data-company-certificate-detail-company]", `${payload.companyName || ""} · ${payload.companyCode || ""}`);
        setDetail("[data-company-certificate-detail-number]", payload.certificateNumber);
        setDetail("[data-company-certificate-detail-status]", payload.statusLabel);
        setDetail("[data-company-certificate-detail-scope]", payload.specialtyLevelScope);
        setDetail("[data-company-certificate-detail-authority]", payload.issuingAuthority);
        setDetail("[data-company-certificate-detail-issued]", payload.issuedOn);
        setDetail("[data-company-certificate-detail-expires]", payload.expiresOn || "长期有效");
        setDetail("[data-company-certificate-detail-notes]", payload.notes);
        const attachment = detailsDialog?.querySelector("[data-company-certificate-detail-attachment]");
        if (attachment) {
            attachment.hidden = !payload.attachmentUrl;
            attachment.href = payload.attachmentUrl || "";
            attachment.dataset.attachmentName = payload.attachmentFileName || "公司证书附件";
            attachment.dataset.attachmentContentType = payload.attachmentContentType || "";
        }
        show(detailsDialog);
    };

    const syncDeleteConfirmation = () => {
        if (!deleteDialog) return;
        const confirmation = deleteDialog.querySelector("[data-company-certificate-delete-confirmation]");
        const submit = deleteDialog.querySelector("[data-company-certificate-delete-submit]");
        if (submit) submit.disabled = confirmation?.value.trim() !== deleteDialog.dataset.expectedText;
    };

    const openDelete = (payload) => {
        if (!deleteDialog || !payload.id) return;
        const id = deleteDialog.querySelector('[name="DeleteInput.Id"]');
        const concurrency = deleteDialog.querySelector('[name="DeleteInput.ConcurrencyStamp"]');
        const expectedInput = deleteDialog.querySelector('[name="DeleteInput.ExpectedText"]');
        const confirmation = deleteDialog.querySelector("[data-company-certificate-delete-confirmation]");
        const expectedLabel = deleteDialog.querySelector("[data-company-certificate-delete-expected]");
        if (id) id.value = payload.id;
        if (concurrency) concurrency.value = payload.concurrencyStamp || "";
        if (expectedInput) expectedInput.value = payload.confirmationText || "";
        if (confirmation) confirmation.value = "";
        if (expectedLabel) expectedLabel.textContent = payload.confirmationText || "";
        deleteDialog.dataset.expectedText = payload.confirmationText || "";
        syncDeleteConfirmation();
        editorDialog?.close();
        show(deleteDialog);
        confirmation?.focus();
    };

    page.querySelectorAll("[data-company-certificate-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            const mode = trigger.dataset.companyCertificateDialogOpen;
            const payload = payloadFrom(trigger);
            if (mode === "details") openDetails(payload);
            else openEditor(mode, payload);
        });
    });
    editorDialog?.querySelector("[data-company-certificate-delete-open]")?.addEventListener("click", (event) => {
        openDelete(JSON.parse(event.currentTarget.dataset.companyCertificatePayload || "{}"));
    });
    deleteDialog?.querySelector("[data-company-certificate-delete-confirmation]")?.addEventListener("input", syncDeleteConfirmation);
    deleteDialog?.querySelectorAll("[data-company-certificate-delete-close]").forEach((button) => {
        button.addEventListener("click", () => {
            deleteDialog.close();
            show(editorDialog);
        });
    });
    page.querySelectorAll("[data-company-certificate-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });
    });

    initAttachmentPreview();

    if (editorDialog?.dataset.dialogOpen === "true") {
        const title = editorDialog.querySelector("[data-company-certificate-editor-title]");
        if (title) title.textContent = field("Id")?.value ? "编辑公司证书" : "新增公司证书";
        show(editorDialog);
    }
    if (deleteDialog?.dataset.dialogOpen === "true") {
        syncDeleteConfirmation();
        show(deleteDialog);
    }
}
