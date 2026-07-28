const page = document.querySelector("[data-employee-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-employee-editor-dialog]");
    const detailsDialog = page.querySelector("[data-employee-details-dialog]");
    const editorForm = editorDialog?.querySelector("[data-employee-editor-form]");
    const field = (name) => editorForm?.querySelector(`[name="Editor.${name}"]`);
    const show = (dialog) => { if (dialog && !dialog.open) dialog.showModal(); };
    const payloadFrom = (trigger) => JSON.parse(trigger.dataset.employeePayload || "{}");
    const setField = (name, value) => {
        const input = field(name);
        if (!input) return;
        if (input.type === "checkbox") input.checked = Boolean(value);
        else input.value = value ?? "";
    };
    const setDetail = (name, value) => {
        const target = detailsDialog?.querySelector(`[data-employee-detail="${name}"]`);
        if (target) target.textContent = value || "未填写";
    };

    const filterForm = page.querySelector(".workbench-inline-filters");
    filterForm?.querySelectorAll("select").forEach((select) => {
        select.addEventListener("change", () => filterForm.requestSubmit());
    });

    const openEditor = (mode, payload = {}) => {
        editorForm?.reset();
        const editing = mode === "edit";
        const copying = mode === "copy";
        setField("Id", editing ? payload.id : "");
        setField("ConcurrencyStamp", editing ? payload.concurrencyStamp : "00000000-0000-0000-0000-000000000000");
        setField("EmployeeNumber", copying ? `${payload.employeeNumber || ""}-COPY` : payload.employeeNumber);
        setField("Name", copying ? `${payload.name || ""}（复制）` : payload.name);
        setField("EmployeeType", payload.employeeType || "Formal");
        setField("Phone", copying ? "" : payload.phone);
        setField("PositionTitle", payload.positionTitle);
        setField("IdentityNumber", copying ? "" : payload.identityNumber);
        setField("BankAccountNumber", copying ? "" : payload.bankAccountNumber);
        setField("BankName", copying ? "" : payload.bankName);
        setField("HireDate", copying ? "" : payload.hireDate);
        setField("LeaveDate", copying ? "" : payload.leaveDate);
        setField("DefaultLegalEntityId", payload.defaultLegalEntityId);
        setField("DefaultMonthlySalary", payload.defaultMonthlySalary);
        setField("DefaultDailyRate", payload.defaultDailyRate);
        setField("DefaultHourlyRate", payload.defaultHourlyRate);
        setField("DefaultPieceworkRate", payload.defaultPieceworkRate);
        setField("IsActive", editing ? payload.isActive : true);
        setField("Notes", payload.notes);
        setField("Reason", `${copying ? "复制" : editing ? "编辑" : "新增"}员工档案`);
        const reason = editorDialog?.querySelector("[data-employee-reason]");
        if (reason) reason.hidden = !editing;
        const title = editorDialog?.querySelector("[data-employee-editor-title]");
        if (title) title.textContent = `${copying ? "复制" : editing ? "编辑" : "新增"}员工`;
        show(editorDialog);
        field("EmployeeNumber")?.focus({ preventScroll: true });
    };

    const openDetails = (payload) => {
        ["name", "employeeNumber", "employeeTypeLabel", "positionTitle", "phone", "company", "department", "hireDate", "leaveDate", "statusLabel", "notes"]
            .forEach((name) => setDetail(name, payload[name]));
        const link = detailsDialog?.querySelector("[data-employee-detail-link]");
        if (link) link.href = payload.detailsUrl || "/Employees";
        show(detailsDialog);
    };

    page.querySelectorAll("[data-employee-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            const mode = trigger.dataset.employeeDialogOpen;
            const payload = payloadFrom(trigger);
            if (mode === "details") openDetails(payload);
            else openEditor(mode, payload);
        });
    });
    page.querySelectorAll("[data-employee-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => { if (event.target === dialog) dialog.close(); });
    });

    if (editorDialog?.dataset.dialogOpen === "true") {
        const editing = Boolean(field("Id")?.value);
        const reason = editorDialog.querySelector("[data-employee-reason]");
        if (reason) reason.hidden = !editing;
        const title = editorDialog.querySelector("[data-employee-editor-title]");
        if (title) title.textContent = `${editing ? "编辑" : "新增"}员工`;
        show(editorDialog);
    }
}
