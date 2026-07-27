const page = document.querySelector("[data-payroll-workspace]");

if (page) {
    const detailsDialog = page.querySelector("[data-payroll-details-dialog]");
    const rosterDialog = page.querySelector("[data-payroll-roster-dialog]");
    const editorDialog = page.querySelector("[data-payroll-editor-dialog]");
    const money = new Intl.NumberFormat("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    let activeRoster = { employees: [], crewWorkers: [], employeeCount: 0, crewCount: 0, totalCount: 0 };
    let activeBatchName = "工资批次";

    const show = (dialog) => {
        if (dialog && !dialog.open) dialog.showModal();
    };

    const payloadFrom = (trigger) => {
        try {
            return JSON.parse(trigger.dataset.payrollPayload || "{}");
        } catch {
            return {};
        }
    };

    const setText = (selector, value, fallback = "未填写") => {
        const target = detailsDialog?.querySelector(selector);
        if (target) target.textContent = value === null || value === undefined || value === "" ? fallback : String(value);
    };

    const openDetails = (payload) => {
        activeRoster = payload.recipientBreakdown || { employees: [], crewWorkers: [], employeeCount: 0, crewCount: 0, totalCount: 0 };
        activeBatchName = payload.name || payload.batchNumber || "工资批次";
        setText("[data-payroll-detail-title]", payload.name, "工资批次详情");
        setText("[data-payroll-detail-number]", payload.batchNumber, "批次记录");
        setText("[data-payroll-detail-date]", payload.paymentDate);
        setText("[data-payroll-detail-status]", payload.statusLabel);
        setText("[data-payroll-detail-project]", payload.project);
        setText("[data-payroll-detail-company]", payload.company);
        setText("[data-payroll-detail-account]", payload.account);
        setText("[data-payroll-detail-voucher]", payload.voucherNumber);
        setText("[data-payroll-detail-recipients]", activeRoster.totalCount ?? payload.recipientCount, "0");
        setText("[data-payroll-detail-recipient-breakdown]", `员工 ${activeRoster.employeeCount || 0} · 班组 ${activeRoster.crewCount || 0}`, "员工 0 · 班组 0");
        setText("[data-payroll-detail-actual]", money.format(Number(payload.actualAmount) || 0), "0.00");
        setText("[data-payroll-detail-employee]", money.format(Number(payload.employeeAmount) || 0), "0.00");
        setText("[data-payroll-detail-crew]", money.format(Number(payload.crewAmount) || 0), "0.00");
        setText("[data-payroll-detail-difference]", money.format(Number(payload.difference) || 0), "0.00");
        setText("[data-payroll-detail-notes]", payload.notes, "暂无备注");
        const edit = detailsDialog?.querySelector("[data-payroll-detail-edit]");
        if (edit) {
            const editUrl = new URL(window.location.href);
            editUrl.searchParams.set("id", payload.id);
            editUrl.searchParams.set("dialog", "editor");
            edit.href = editUrl;
        }
        show(detailsDialog);
    };

    const renderRosterItems = (target, items, emptyText) => {
        if (!target) return;
        target.replaceChildren();
        if (!items.length) {
            const empty = document.createElement("p");
            empty.className = "payroll-roster-empty";
            empty.textContent = emptyText;
            target.appendChild(empty);
            return;
        }
        items.forEach((item) => {
            const row = document.createElement("article");
            row.className = "payroll-roster-row";
            const identity = document.createElement("div");
            const name = document.createElement("strong");
            name.textContent = item.name || "未命名人员";
            identity.appendChild(name);
            if (item.groupName) {
                const group = document.createElement("span");
                group.textContent = item.groupName;
                identity.appendChild(group);
            }
            const amount = document.createElement("strong");
            amount.className = "payroll-roster-amount";
            amount.textContent = money.format(Number(item.amount) || 0);
            row.append(identity, amount);
            target.appendChild(row);
        });
    };

    const renderRoster = () => {
        const batch = rosterDialog?.querySelector("[data-payroll-roster-batch]");
        const employeeCount = rosterDialog?.querySelector("[data-payroll-roster-employee-count]");
        const crewCount = rosterDialog?.querySelector("[data-payroll-roster-crew-count]");
        if (batch) batch.textContent = activeBatchName;
        if (employeeCount) employeeCount.textContent = `${activeRoster.employeeCount || 0} 人`;
        if (crewCount) crewCount.textContent = `${activeRoster.crewCount || 0} 人`;
        renderRosterItems(rosterDialog?.querySelector("[data-payroll-roster-employees]"), activeRoster.employees || [], "本批次没有员工发放记录。");
        renderRosterItems(rosterDialog?.querySelector("[data-payroll-roster-crews]"), activeRoster.crewWorkers || [], "本批次没有班组人员发放记录。");
        show(rosterDialog);
    };

    const activateEditorTab = (root, name) => {
        root.querySelectorAll("[data-payroll-editor-tab]").forEach((tab) => {
            const active = tab.dataset.payrollEditorTab === name;
            tab.classList.toggle("is-active", active);
            tab.setAttribute("aria-selected", String(active));
        });
        root.querySelectorAll("[data-payroll-editor-panel]").forEach((panel) => {
            panel.hidden = panel.dataset.payrollEditorPanel !== name;
        });
    };

    const updateReconciliation = (root) => {
        let detail = 0;
        root.querySelectorAll("[data-payroll-amount]").forEach((input) => {
            const selected = input.closest("tr")?.querySelector("input[type='checkbox']");
            if (selected?.checked) detail += Number(input.value || 0);
        });
        const actual = Number(root.querySelector("[data-payroll-actual]")?.value || 0);
        const detailTarget = root.querySelector("[data-payroll-detail-total]");
        const actualTarget = root.querySelector("[data-payroll-actual-total]");
        const differenceTarget = root.querySelector("[data-payroll-difference]");
        if (detailTarget) detailTarget.textContent = detail.toFixed(2);
        if (actualTarget) actualTarget.textContent = actual.toFixed(2);
        if (differenceTarget) {
            const difference = actual - detail;
            differenceTarget.textContent = difference.toFixed(2);
            differenceTarget.classList.toggle("is-balanced", difference === 0);
        }
    };

    const initializeEditor = (root) => {
        root.querySelectorAll("[data-payroll-editor-tab]").forEach((tab) => {
            tab.addEventListener("click", () => activateEditorTab(root, tab.dataset.payrollEditorTab));
        });
        root.addEventListener("input", () => updateReconciliation(root));
        root.addEventListener("change", () => updateReconciliation(root));
        updateReconciliation(root);

        const highlighted = root.querySelector("[data-payroll-line].is-highlighted");
        if (highlighted) {
            const panel = highlighted.closest("[data-payroll-editor-panel]");
            activateEditorTab(root, panel?.dataset.payrollEditorPanel || "employees");
            requestAnimationFrame(() => highlighted.scrollIntoView({ block: "center" }));
        } else {
            activateEditorTab(root, "employees");
        }
    };

    const filterForm = page.querySelector(".workbench-inline-filters");
    filterForm?.querySelectorAll("select").forEach((select) => {
        select.addEventListener("change", () => filterForm.requestSubmit());
    });

    page.querySelectorAll("[data-payroll-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            if (trigger.dataset.payrollDialogOpen === "details") openDetails(payloadFrom(trigger));
        });
    });
    page.querySelector("[data-payroll-roster-open]")?.addEventListener("click", renderRoster);
    page.querySelectorAll("[data-payroll-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });
    });

    const editor = editorDialog?.querySelector("[data-payroll-editor]");
    if (editor) initializeEditor(editor);
    if (editorDialog?.dataset.dialogOpen === "true") show(editorDialog);

    if (page.dataset.activeDialog === "details" && page.dataset.activeId) {
        const trigger = page.querySelector(`[data-payroll-dialog-open='details'][data-payroll-id='${CSS.escape(page.dataset.activeId)}']`);
        if (trigger) openDetails(payloadFrom(trigger));
    }
}
