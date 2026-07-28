const page = document.querySelector("[data-payroll-workspace]");

if (page) {
    const detailsDialog = page.querySelector("[data-payroll-details-dialog]");
    const rosterDialog = page.querySelector("[data-payroll-roster-dialog]");
    const editorDialog = page.querySelector("[data-payroll-editor-dialog]");
    const money = new Intl.NumberFormat("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    let activeRoster = {
        employees: [],
        temporaryWorkers: [],
        crewWorkers: [],
        employeeCount: 0,
        temporaryCount: 0,
        crewCount: 0,
        employeeAmount: 0,
        temporaryAmount: 0,
        crewAmount: 0,
        canViewSensitive: false,
        totalCount: 0
    };
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
        activeRoster = payload.recipientBreakdown || {
            employees: [], temporaryWorkers: [], crewWorkers: [],
            employeeCount: 0, temporaryCount: 0, crewCount: 0,
            employeeAmount: 0, temporaryAmount: 0, crewAmount: 0,
            canViewSensitive: false, totalCount: 0
        };
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
        setText("[data-payroll-detail-recipient-breakdown]", `员工 ${activeRoster.employeeCount || 0} · 临时 ${activeRoster.temporaryCount || 0} · 班组 ${activeRoster.crewCount || 0}`, "员工 0 · 临时 0 · 班组 0");
        setText("[data-payroll-detail-actual]", money.format(Number(payload.actualAmount) || 0), "0.00");
        setText("[data-payroll-detail-employee-count]", `${activeRoster.employeeCount || 0} 人`, "0 人");
        setText("[data-payroll-detail-temporary-count]", `${activeRoster.temporaryCount || 0} 人`, "0 人");
        setText("[data-payroll-detail-crew-count]", `${activeRoster.crewCount || 0} 人`, "0 人");
        setText("[data-payroll-detail-employee]", money.format(Number(activeRoster.employeeAmount) || 0), "0.00");
        setText("[data-payroll-detail-temporary]", money.format(Number(activeRoster.temporaryAmount) || 0), "0.00");
        setText("[data-payroll-detail-crew]", money.format(Number(activeRoster.crewAmount) || 0), "0.00");
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
            const row = document.createElement("tr");
            const empty = document.createElement("td");
            empty.className = "payroll-roster-empty";
            empty.colSpan = activeRoster.canViewSensitive ? 5 : 2;
            empty.textContent = emptyText;
            row.appendChild(empty);
            target.appendChild(row);
            return;
        }
        items.forEach((item) => {
            const row = document.createElement("tr");
            row.className = "payroll-roster-row";
            const identity = document.createElement("td");
            identity.className = "payroll-roster-person";
            const name = document.createElement("strong");
            name.textContent = item.name || "未命名人员";
            identity.appendChild(name);
            const details = [item.personNumber, item.typeName, item.groupName, item.roleName, item.phone].filter(Boolean);
            if (details.length) {
                const metadata = document.createElement("span");
                metadata.textContent = details.join(" · ");
                identity.appendChild(metadata);
            }
            const sensitiveCell = (value) => {
                const cell = document.createElement("td");
                cell.setAttribute("data-payroll-sensitive-column", "");
                cell.hidden = !activeRoster.canViewSensitive;
                cell.textContent = value || "未填写";
                return cell;
            };
            const amount = document.createElement("td");
            amount.className = "payroll-roster-amount";
            amount.textContent = money.format(Number(item.amount) || 0);
            row.append(
                identity,
                sensitiveCell(item.identityNumber),
                sensitiveCell(item.bankAccountNumber),
                sensitiveCell(item.bankName),
                amount
            );
            target.appendChild(row);
        });
    };

    const renderRoster = (category) => {
        const categories = {
            employees: { title: "员工详细名单", summary: "员工分类汇总", items: activeRoster.employees || [], count: activeRoster.employeeCount || 0, amount: activeRoster.employeeAmount || 0, empty: "本批次没有员工发放记录。" },
            temporaryWorkers: { title: "临时人员详细名单", summary: "临时人员分类汇总", items: activeRoster.temporaryWorkers || [], count: activeRoster.temporaryCount || 0, amount: activeRoster.temporaryAmount || 0, empty: "本批次没有临时人员发放记录。" },
            crewWorkers: { title: "班组详细名单", summary: "班组分类汇总", items: activeRoster.crewWorkers || [], count: activeRoster.crewCount || 0, amount: activeRoster.crewAmount || 0, empty: "本批次没有班组人员发放记录。" }
        };
        const selected = categories[category] || categories.employees;
        const batch = rosterDialog?.querySelector("[data-payroll-roster-batch]");
        const title = rosterDialog?.querySelector("[data-payroll-roster-title]");
        const summary = rosterDialog?.querySelector("[data-payroll-roster-summary-label]");
        const count = rosterDialog?.querySelector("[data-payroll-roster-count]");
        const amount = rosterDialog?.querySelector("[data-payroll-roster-amount]");
        if (batch) batch.textContent = activeBatchName;
        if (title) title.textContent = selected.title;
        if (summary) summary.textContent = selected.summary;
        if (count) count.textContent = `${selected.count} 人`;
        if (amount) amount.textContent = money.format(Number(selected.amount) || 0);
        rosterDialog?.querySelector("[data-payroll-roster-table]")?.classList.toggle("is-sensitive-hidden", !activeRoster.canViewSensitive);
        rosterDialog?.querySelectorAll("[data-payroll-sensitive-column]").forEach((cell) => {
            cell.hidden = !activeRoster.canViewSensitive;
        });
        renderRosterItems(rosterDialog?.querySelector("[data-payroll-roster-items]"), selected.items, selected.empty);
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
    page.querySelectorAll("[data-payroll-roster-open]").forEach((button) => {
        button.addEventListener("click", () => renderRoster(button.dataset.payrollRosterCategory));
    });
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
