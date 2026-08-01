const page = document.querySelector("[data-employee-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-employee-editor-dialog]");
    const detailsDialog = page.querySelector("[data-employee-details-dialog]");
    const editorForm = editorDialog?.querySelector("[data-employee-editor-form]");
    const nextEmployeeNumber = page.dataset.nextEmployeeNumber || "";
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
    const money = new Intl.NumberFormat("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

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
        setField("EmployeeNumber", editing ? payload.employeeNumber : nextEmployeeNumber);
        setField("Name", copying ? `${payload.name || ""}（副本）` : payload.name);
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
        ["name", "employeeNumber", "employeeTypeLabel", "positionTitle", "phone", "company", "department", "project", "crew", "affiliationPosition", "identityNumber", "bankAccountNumber", "bankName", "hireDate", "leaveDate", "statusLabel", "notes"]
            .forEach((name) => setDetail(name, payload[name]));
        detailsDialog?.querySelectorAll("[data-employee-money]").forEach((target) => {
            const value = Number(payload[target.dataset.employeeMoney]);
            target.textContent = Number.isFinite(value) ? money.format(value) : "0.00";
        });
        detailsDialog?.querySelector(".employee-dialog-metric--balance")?.classList.toggle("is-danger", Boolean(payload.isOverpaid));
        setDetail("settlementProgressLabel", `${Number(payload.settlementProgressPercent || 0).toFixed(2)}%`);
        const missing = [];
        if (!payload.phone) missing.push("联系电话未填写");
        if (!payload.company) missing.push("当前公司未归属");
        if (!payload.positionTitle && !payload.affiliationPosition) missing.push("岗位未填写");
        setDetail("riskLabel", payload.isOverpaid ? "存在超付或负余额，请核对付款来源。" : missing.join("；") || "当前未发现需处理的风险。");
        const link = detailsDialog?.querySelector("[data-employee-detail-link]");
        if (link) link.href = payload.detailsUrl || "/Employees";
        const edit = detailsDialog?.querySelector("[data-employee-detail-edit]");
        if (edit) edit.href = payload.editUrl || payload.detailsUrl || "/Employees";
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
