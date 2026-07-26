import { initAttachmentPreview } from "../components/attachment-preview.js";

const page = document.querySelector("[data-equipment-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-equipment-editor-dialog]");
    const detailsDialog = page.querySelector("[data-equipment-details-dialog]");
    const usageDialog = page.querySelector("[data-equipment-usage-dialog]");
    const deleteDialog = page.querySelector("[data-equipment-delete-dialog]");
    const editorForm = editorDialog?.querySelector("[data-equipment-editor-form]");
    const usageForm = usageDialog?.querySelector("[data-equipment-usage-form]");
    const usageHistory = usageDialog?.querySelector("[data-equipment-usage-history]");
    const usageEditor = usageDialog?.querySelector("[data-equipment-usage-editor]");
    let currentUsageEquipment = null;

    const preserveWorkspaceScope = () => {
        const form = page.querySelector(".workbench-inline-filters");
        if (!form) return;
        const companySelect = form.querySelector('[name="CompanyId"]');
        companySelect?.addEventListener("change", () => form.requestSubmit());
        const scope = [
            ["CompanyId", page.dataset.companyId]
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
    const usageField = (name) => usageForm?.querySelector(`[name="UsageInput.${name}"]`);
    const setUsageField = (name, value) => {
        const input = usageField(name);
        if (input) input.value = value ?? "";
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
        if (ownership !== "SelfOwned" && owner) owner.value = "";
        if (ownership !== "Rented" && lessor) lessor.value = "";
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
        setField("Status", copy ? "Idle" : payload.status || "Idle");
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
        const deleteButton = editorDialog?.querySelector("[data-equipment-delete-open]");
        if (deleteButton) {
            deleteButton.hidden = mode !== "edit";
            deleteButton.dataset.equipmentPayload = mode === "edit" ? JSON.stringify(payload) : "";
        }
        showCurrentAttachment(copy ? {} : payload);
        syncOwnership();
        show(editorDialog);
    };

    const openDetails = (payload) => {
        text("[data-equipment-detail-number]", `${payload.equipmentNumber || ""} · 设备档案`);
        text("[data-equipment-detail-name]", payload.name);
        text("[data-equipment-detail-model]", `${payload.model || "未填写型号"} / ${payload.category || "未分类"}`);
        text("[data-equipment-detail-company]", payload.managingLegalEntityName || "待分配");
        const owner = payload.ownershipType === "SelfOwned"
            ? payload.ownerLegalEntityName
            : payload.ownershipType === "Rented"
                ? payload.lessorBusinessPartnerName
                : "不适用";
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
            attachment.dataset.attachmentName = payload.qualificationAttachmentFileName || "设备合格证";
            const extension = (payload.qualificationAttachmentFileName || "").split(".").pop()?.toLowerCase();
            attachment.dataset.attachmentContentType = extension === "pdf"
                ? "application/pdf"
                : ["jpg", "jpeg", "png"].includes(extension) ? `image/${extension === "jpg" ? "jpeg" : extension}` : "";
        }
        show(detailsDialog);
    };

    const setUsagePeriods = (periods = []) => {
        const container = usageForm?.querySelector("[data-equipment-usage-periods]");
        if (!container) return;
        container.replaceChildren();
        periods.forEach((period, index) => {
            [
                ["StartDate", period.startDate],
                ["EndDate", period.endDate],
                ["PeriodType", period.periodType],
                ["IsChargeable", String(Boolean(period.isChargeable))],
                ["Notes", period.notes || ""]
            ].forEach(([name, value]) => {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = `UsageInput.Periods[${index}].${name}`;
                input.value = value;
                container.appendChild(input);
            });
        });
    };

    const showUsageHistory = () => {
        if (usageHistory) usageHistory.hidden = false;
        if (usageEditor) usageEditor.hidden = true;
    };

    const showUsageEditor = (payload = null) => {
        usageForm?.reset();
        const editing = Boolean(payload?.id);
        setUsageField("Id", editing ? payload.id : "");
        setUsageField("ConcurrencyStamp", editing ? payload.concurrencyStamp : "");
        setUsageField("EquipmentId", payload?.equipmentId || currentUsageEquipment?.id);
        setUsageField("ProjectId", payload?.projectId || "");
        setUsageField("LegalEntityId", payload?.legalEntityId || currentUsageEquipment?.managingLegalEntityId);
        setUsageField("EntryDate", payload?.entryDate || new Date().toISOString().slice(0, 10));
        setUsageField("ExitDate", payload?.exitDate || "");
        setUsageField("RentMode", payload?.rentMode || "Daily");
        setUsageField("UnitRate", payload?.unitRate ?? currentUsageEquipment?.internalDailyRate ?? 0);
        setUsageField("Reason", editing ? "编辑设备进退场" : "登记设备进退场");
        setUsagePeriods(payload?.periods || []);
        const title = usageForm?.querySelector("[data-equipment-usage-editor-title]");
        if (title) title.textContent = editing ? "编辑记录" : "新增记录";
        if (usageHistory) usageHistory.hidden = true;
        if (usageEditor) usageEditor.hidden = false;
    };

    const filterUsageRows = (equipmentId) => {
        let visibleCount = 0;
        usageDialog?.querySelectorAll("[data-equipment-usage-row]").forEach((row) => {
            const matches = row.dataset.equipmentId?.toLowerCase() === equipmentId?.toLowerCase();
            row.hidden = !matches;
            if (matches) visibleCount += 1;
        });
        const empty = usageDialog?.querySelector("[data-equipment-usage-empty]");
        if (empty) empty.hidden = visibleCount > 0;
    };

    const openUsage = (payload) => {
        if (page.dataset.openUsageEquipmentId?.toLowerCase() !== payload.id?.toLowerCase()) {
            const url = new URL(window.location.href);
            url.searchParams.set("OpenUsageEquipmentId", payload.id || "");
            window.location.assign(url);
            return;
        }
        currentUsageEquipment = payload;
        const name = usageDialog?.querySelector("[data-equipment-usage-equipment-name]");
        if (name) name.textContent = `${payload.equipmentNumber || ""} · ${payload.name || ""}`;
        const openId = usageDialog?.querySelector("[data-equipment-open-usage-id]");
        if (openId) openId.value = payload.id || "";
        filterUsageRows(payload.id);
        showUsageHistory();
        show(usageDialog);
    };

    const openDelete = (payload) => {
        if (!deleteDialog || !payload?.id) return;
        const id = deleteDialog.querySelector('[name="DeleteInput.Id"]');
        const concurrency = deleteDialog.querySelector('[name="DeleteInput.ConcurrencyStamp"]');
        const equipmentNumber = deleteDialog.querySelector('[name="DeleteInput.EquipmentNumber"]');
        const confirmation = deleteDialog.querySelector("[data-equipment-delete-confirmation]");
        const expected = deleteDialog.querySelector("[data-equipment-delete-number]");
        const submit = deleteDialog.querySelector("[data-equipment-delete-submit]");
        if (id) id.value = payload.id;
        if (concurrency) concurrency.value = payload.concurrencyStamp || "";
        if (equipmentNumber) equipmentNumber.value = payload.equipmentNumber || "";
        if (confirmation) confirmation.value = "";
        if (expected) expected.textContent = payload.equipmentNumber || "";
        if (submit) submit.disabled = true;
        deleteDialog.dataset.expectedNumber = payload.equipmentNumber || "";
        editorDialog?.close();
        show(deleteDialog);
        confirmation?.focus();
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

    editorDialog?.querySelector("[data-equipment-delete-open]")?.addEventListener("click", (event) => {
        openDelete(JSON.parse(event.currentTarget.dataset.equipmentPayload || "{}"));
    });
    deleteDialog?.querySelector("[data-equipment-delete-confirmation]")?.addEventListener("input", (event) => {
        const submit = deleteDialog.querySelector("[data-equipment-delete-submit]");
        if (submit) submit.disabled = event.currentTarget.value.trim() !== deleteDialog.dataset.expectedNumber;
    });
    deleteDialog?.querySelectorAll("[data-equipment-delete-close]").forEach((button) => {
        button.addEventListener("click", () => {
            deleteDialog.close();
            show(editorDialog);
        });
    });

    usageDialog?.querySelector("[data-equipment-usage-create]")?.addEventListener("click", () => showUsageEditor());
    usageDialog?.querySelectorAll("[data-equipment-usage-edit]").forEach((button) => {
        button.addEventListener("click", () => showUsageEditor(JSON.parse(button.dataset.equipmentUsagePayload || "{}")));
    });
    usageDialog?.querySelectorAll("[data-equipment-usage-cancel-edit]").forEach((button) => {
        button.addEventListener("click", showUsageHistory);
    });
    usageDialog?.querySelector("[data-equipment-usage-year]")?.addEventListener("change", () => {
        usageDialog.querySelector("[data-equipment-usage-year-filter]")?.requestSubmit();
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
    initAttachmentPreview();

    if (editorDialog?.dataset.dialogOpen === "true") show(editorDialog);
    if (deleteDialog?.dataset.dialogOpen === "true") show(deleteDialog);
    const openUsageEquipmentId = page.dataset.openUsageEquipmentId;
    if (openUsageEquipmentId) {
        const trigger = Array.from(page.querySelectorAll('[data-equipment-dialog-open="usage"]'))
            .find((button) => payloadFrom(button).id?.toLowerCase() === openUsageEquipmentId.toLowerCase());
        currentUsageEquipment = trigger ? payloadFrom(trigger) : { id: openUsageEquipmentId };
        const name = usageDialog?.querySelector("[data-equipment-usage-equipment-name]");
        if (name && trigger) name.textContent = `${currentUsageEquipment.equipmentNumber || ""} · ${currentUsageEquipment.name || ""}`;
        filterUsageRows(openUsageEquipmentId);
        if (usageDialog?.dataset.usageEditorOpen === "true") {
            if (usageHistory) usageHistory.hidden = true;
            if (usageEditor) usageEditor.hidden = false;
        } else {
            showUsageHistory();
        }
        show(usageDialog);
    } else if (usageDialog?.dataset.dialogOpen === "true") {
        show(usageDialog);
    }
}
