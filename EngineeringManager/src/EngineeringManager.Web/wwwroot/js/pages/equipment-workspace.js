const page = document.querySelector("[data-equipment-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-equipment-editor-dialog]");
    const detailsDialog = page.querySelector("[data-equipment-details-dialog]");
    const usageDialog = page.querySelector("[data-equipment-usage-dialog]");
    const editorForm = editorDialog?.querySelector("[data-equipment-editor-form]")
        ?? page.querySelector("[data-equipment-editor-form]");

    const preserveWorkspaceScope = () => {
        const form = page.querySelector(".workbench-inline-filters");
        if (!form) return;
        const scope = [
            ["CompanyId", page.dataset.companyId],
            ["Unassigned", page.dataset.unassigned === "true" ? "true" : ""]
        ];
        scope.forEach(([name, value]) => {
            if (!value || form.querySelector(`input[name="${name}"]`)) return;
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = name;
            input.value = value;
            form.appendChild(input);
        });
        const clearLink = page.querySelector(".workbench-inline-clear");
        if (!clearLink) return;
        const clearUrl = new URL(clearLink.href, window.location.href);
        scope.forEach(([name, value]) => {
            if (value) clearUrl.searchParams.set(name, value);
        });
        clearLink.href = clearUrl.toString();
    };

    const field = (name) => editorForm?.querySelector(`[name="Editor.${name}"], [name="Input.${name}"]`);
    const setField = (name, value) => {
        const input = field(name);
        if (!input) return;
        if (input.type === "checkbox") input.checked = Boolean(value);
        else input.value = value ?? "";
    };
    const text = (selector, value) => {
        const target = detailsDialog?.querySelector(selector);
        if (target) target.textContent = value || "未填写";
    };
    const show = (dialog) => {
        if (dialog && !dialog.open) dialog.showModal();
    };
    const payloadFrom = (button) => JSON.parse(button.dataset.equipmentPayload || "{}");

    preserveWorkspaceScope();

    const syncOwnership = () => {
        const ownership = field("OwnershipType")?.value;
        const selfOwned = editorForm?.querySelector("[data-equipment-self-owned]");
        const rented = editorForm?.querySelector("[data-equipment-rented]");
        if (selfOwned) selfOwned.hidden = ownership !== "SelfOwned";
        if (rented) rented.hidden = ownership !== "Rented";
        const owner = field("OwnerLegalEntityId");
        const lessor = field("LessorBusinessPartnerId");
        if (ownership === "SelfOwned" && lessor) lessor.value = "";
        if (ownership === "Rented" && owner) owner.value = "";
    };

    const showCurrentAttachment = (payload) => {
        const shell = editorForm?.querySelector("[data-equipment-current-attachment]");
        const name = editorForm?.querySelector("[data-equipment-attachment-name]");
        if (!shell || !name) return;
        const fileName = payload.qualificationAttachmentFileName;
        shell.hidden = !fileName;
        name.textContent = fileName || "";
    };

    const openEditor = (mode, payload = {}) => {
        editorForm?.reset();
        const copy = mode === "copy";
        setField("Id", mode === "edit" ? payload.id : "");
        setField("ConcurrencyStamp", mode === "edit" ? payload.concurrencyStamp : "");
        setField("EquipmentNumber", copy ? "" : payload.equipmentNumber);
        setField("Name", copy ? `${payload.name || ""} - 副本` : payload.name);
        setField("Model", payload.model);
        setField("Category", payload.category);
        setField("OwnershipType", payload.ownershipType || "SelfOwned");
        setField("ManagingLegalEntityId", payload.managingLegalEntityId);
        setField("OwnerLegalEntityId", payload.ownerLegalEntityId);
        setField("LessorBusinessPartnerId", payload.lessorBusinessPartnerId);
        setField("PurchaseDate", payload.purchaseDate);
        setField("PurchaseAmount", payload.purchaseAmount);
        setField("InternalDailyRate", payload.internalDailyRate);
        setField("QualificationCertificateNumber", payload.qualificationCertificateNumber);
        setField("QualificationIssuedOn", payload.qualificationIssuedOn);
        setField("QualificationExpiresOn", payload.qualificationExpiresOn);
        setField("QualificationAttachmentId", copy ? "" : payload.qualificationAttachmentId);
        setField("QualificationAttachmentFileName", copy ? "" : payload.qualificationAttachmentFileName);
        setField("RemoveQualificationAttachment", false);
        setField("IsActive", payload.isActive ?? true);
        setField("Notes", payload.notes);
        setField("Reason", copy ? "复制设备档案" : mode === "edit" ? "维护设备档案" : "新增设备档案");
        const title = editorDialog?.querySelector("[data-equipment-editor-title]");
        if (title) title.textContent = copy ? "复制设备" : mode === "edit" ? "编辑设备" : "新增设备";
        showCurrentAttachment(copy ? {} : payload);
        syncOwnership();
        show(editorDialog);
    };

    const openDetails = (payload) => {
        text("[data-equipment-detail-number]", `${payload.equipmentNumber || ""} · 设备详情`);
        text("[data-equipment-detail-name]", payload.name);
        text("[data-equipment-detail-model]", `${payload.model || "未填写型号"} / ${payload.category || "未分类"}`);
        text("[data-equipment-detail-company]", payload.managingLegalEntityName || "待分配");
        const owner = payload.ownershipType === "SelfOwned" ? payload.ownerLegalEntityName : payload.lessorBusinessPartnerName;
        text("[data-equipment-detail-ownership]", `${payload.ownershipLabel || ""} · ${owner || "未填写"}`);
        text("[data-equipment-detail-status]", `${payload.statusLabel || ""} · ${payload.isActive ? "档案启用" : "档案停用"}`);
        text("[data-equipment-detail-purchase]", `${payload.purchaseDate || "未填写日期"} · ${payload.purchaseAmount == null ? "未填写金额" : `${payload.purchaseAmount} 元`}`);
        text("[data-equipment-detail-rate]", payload.internalDailyRate == null ? "未填写" : `${payload.internalDailyRate} 元/日`);
        text("[data-equipment-detail-certificate]", `${payload.qualificationCertificateNumber || "未登记"} · ${payload.qualificationExpiresOn || "长期/未填写有效期"}`);
        text("[data-equipment-detail-notes]", payload.notes || "未填写");
        const attachment = detailsDialog?.querySelector("[data-equipment-detail-attachment]");
        if (attachment) {
            attachment.hidden = !payload.qualificationAttachmentId;
            attachment.href = payload.qualificationAttachmentId
                ? `?handler=QualificationAttachment&equipmentId=${encodeURIComponent(payload.id)}`
                : "";
        }
        show(detailsDialog);
    };

    const openUsage = (payload) => {
        const form = usageDialog?.querySelector("form");
        form?.reset();
        const setUsage = (name, value) => {
            const input = form?.querySelector(`[name="UsageInput.${name}"]`);
            if (input) input.value = value ?? "";
        };
        setUsage("EquipmentId", payload.id);
        setUsage("LegalEntityId", payload.managingLegalEntityId);
        setUsage("UnitRate", payload.internalDailyRate);
        show(usageDialog);
    };

    page.querySelectorAll("[data-equipment-dialog-open]").forEach((button) => {
        button.addEventListener("click", () => {
            const mode = button.dataset.equipmentDialogOpen;
            const payload = payloadFrom(button);
            if (mode === "create" || mode === "edit" || mode === "copy") openEditor(mode, payload);
            else if (mode === "details") openDetails(payload);
            else if (mode === "usage") openUsage(payload);
        });
    });

    page.querySelectorAll("[data-equipment-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });
    });

    field("OwnershipType")?.addEventListener("change", syncOwnership);
    field("ManagingLegalEntityId")?.addEventListener("change", () => {
        if (field("OwnershipType")?.value === "SelfOwned" && !field("OwnerLegalEntityId")?.value)
            setField("OwnerLegalEntityId", field("ManagingLegalEntityId")?.value);
    });
    syncOwnership();

    if (editorDialog?.dataset.dialogOpen === "true") show(editorDialog);
    if (usageDialog?.dataset.dialogOpen === "true") show(usageDialog);
}
