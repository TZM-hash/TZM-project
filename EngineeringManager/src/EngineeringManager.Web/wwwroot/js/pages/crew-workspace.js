const page = document.querySelector("[data-crew-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-crew-editor-dialog]");
    const detailsDialog = page.querySelector("[data-crew-details-dialog]");
    const rosterDialog = page.querySelector("[data-crew-roster-dialog]");
    const financeDialog = page.querySelector("[data-crew-finance-dialog]");
    const editorForm = editorDialog?.querySelector("[data-crew-editor-form]");
    const statusSection = editorDialog?.querySelector("[data-crew-status-section]");
    const nextPartnerNumber = page.dataset.nextPartnerNumber || "";
    const rosterBody = rosterDialog?.querySelector("[data-crew-roster-table-body]");
    let rosterRequestVersion = 0;
    const money = new Intl.NumberFormat("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    const show = (dialog) => {
        if (dialog && !dialog.open) dialog.showModal();
    };
    const payloadFrom = (trigger) => JSON.parse(trigger.dataset.crewPayload || "{}");
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
    const valueAt = (source, path) =>
        path.split(".").reduce((value, key) => value?.[key], source) ?? 0;
    const formatMoney = (value) => money.format(Number(value) || 0);
    const progressStateFor = (targetAmount, completedAmount) => {
        if (targetAmount <= 0) return completedAmount > 0 ? "over" : "no-target";

        const rawPercentage = Math.max(completedAmount / targetAmount * 100, 0);
        if (rawPercentage < 30) return "critical";
        if (rawPercentage < 60) return "low";
        if (rawPercentage < 85) return "medium";
        if (rawPercentage < 100) return "near";
        if (rawPercentage === 100) return "complete";
        return "over";
    };

    const form = page.querySelector(".workbench-inline-filters");
    if (form) {
        form.querySelectorAll("select").forEach((select) => {
            select.addEventListener("change", () => form.requestSubmit());
        });
    }

    const openEditor = (mode, payload = {}) => {
        editorForm?.reset();
        const copy = mode === "copy";
        const editing = mode === "edit";
        if (statusSection) statusSection.hidden = !editing;
        setField("Id", editing ? payload.id : "");
        setField("ConcurrencyStamp", editing ? payload.concurrencyStamp : "00000000-0000-0000-0000-000000000000");
        setField("PartnerNumber", editing ? payload.partnerNumber : nextPartnerNumber);
        setField("Name", copy ? `${payload.name}（副本）` : payload.name);
        setField("ShortName", copy ? `${payload.shortName}副本` : payload.shortName);
        setField("UnifiedSocialCreditCode", copy ? "" : payload.unifiedSocialCreditCode);
        setField("TradeCategory", payload.tradeCategory);
        setField("ContactName", copy ? "" : payload.contactName);
        setField("ContactPhone", copy ? "" : payload.contactPhone);
        setField("ContactNotes", copy ? "" : payload.contactNotes);
        setField("Notes", payload.notes);
        setField("IsActive", editing ? payload.isActive : true);
        setField("Reason", copy ? "复制施工班组" : editing ? "修改施工班组" : "新增施工班组");
        const title = editorDialog?.querySelector("[data-crew-editor-title]");
        if (title) title.textContent = copy ? "复制施工班组" : editing ? "编辑施工班组" : "新增施工班组";
        show(editorDialog);
    };

    const openDetails = (payload) => {
        setDetail("[data-crew-detail-title]", payload.name);
        setDetail("[data-crew-detail-number]", payload.partnerNumber);
        setDetail("[data-crew-detail-short-name]", payload.shortName);
        setDetail("[data-crew-detail-credit-code]", payload.unifiedSocialCreditCode);
        setDetail("[data-crew-detail-role]", payload.roleLabel);
        setDetail("[data-crew-detail-trade]", payload.tradeCategory);
        setDetail("[data-crew-detail-contact]", payload.contactName);
        setDetail("[data-crew-detail-phone]", payload.contactPhone);
        setDetail("[data-crew-detail-projects]", String(payload.projectCount ?? 0));
        setDetail("[data-crew-detail-status]", payload.statusLabel);
        setDetail("[data-crew-detail-notes]", payload.notes);
        setDetail("[data-crew-detail-contact-notes]", payload.contactNotes);
        const roster = detailsDialog?.querySelector("[data-crew-detail-roster]");
        const finance = detailsDialog?.querySelector("[data-crew-detail-finance]");
        if (roster) roster.href = payload.rosterUrl || "";
        if (finance) finance.href = payload.financeUrl || "";
        show(detailsDialog);
    };

    const renderRosterMessage = (message, isError = false) => {
        if (!rosterBody) return;
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = 5;
        cell.className = isError ? "empty-state crew-roster-error" : "empty-state";
        cell.textContent = message;
        row.append(cell);
        rosterBody.replaceChildren(row);
    };

    const rosterCell = (value, fallback = "未填写") => {
        const cell = document.createElement("td");
        cell.textContent = value || fallback;
        return cell;
    };

    const openRoster = async (payload) => {
        if (!rosterDialog || !rosterBody) return;

        const requestVersion = ++rosterRequestVersion;
        const title = rosterDialog.querySelector("[data-crew-roster-title]");
        const number = rosterDialog.querySelector("[data-crew-roster-number]");
        const manage = rosterDialog.querySelector("[data-crew-roster-manage]");
        if (title) title.textContent = payload.name || "施工班组人员";
        if (number) number.textContent = payload.partnerNumber || "人员名册";
        if (manage) manage.href = payload.rosterUrl || "/Crews/Details";
        renderRosterMessage("正在加载人员名册…");
        show(rosterDialog);

        try {
            const response = await fetch(`${window.location.pathname}?handler=Roster&id=${encodeURIComponent(payload.id)}`, {
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error(`Roster request failed: ${response.status}`);
            const data = await response.json();
            if (requestVersion !== rosterRequestVersion) return;

            const setMetric = (selector, value) => {
                const target = rosterDialog.querySelector(selector);
                if (target) target.textContent = String(value ?? 0);
            };
            setMetric("[data-crew-roster-current]", data.currentWorkerCount);
            setMetric("[data-crew-roster-history]", data.historicalWorkerCount);
            setMetric("[data-crew-roster-projects]", data.projectCount);

            if (!data.workers?.length) {
                renderRosterMessage("当前班组暂无人员记录。");
                return;
            }

            const rows = data.workers.map((worker) => {
                const row = document.createElement("tr");
                row.append(
                    rosterCell(worker.name),
                    rosterCell(worker.phone),
                    rosterCell(worker.trade),
                    rosterCell(worker.startDate)
                );
                const statusCell = document.createElement("td");
                const status = document.createElement("span");
                status.className = `crew-roster-status ${worker.isCurrent ? "is-current" : "is-former"}`;
                status.textContent = worker.isCurrent ? "在组" : "已退出";
                statusCell.append(status);
                if (worker.endDate) {
                    const endDate = document.createElement("small");
                    endDate.textContent = worker.endDate;
                    statusCell.append(endDate);
                }
                row.append(statusCell);
                return row;
            });
            rosterBody.replaceChildren(...rows);
        } catch {
            if (requestVersion === rosterRequestVersion) renderRosterMessage("人员名册加载失败，请稍后重试。", true);
        }
    };

    const openFinance = (payload) => {
        if (!financeDialog) return;

        const title = financeDialog.querySelector("[data-crew-finance-title]");
        const number = financeDialog.querySelector("[data-crew-finance-number]");
        if (title) title.textContent = payload.name || "施工班组财务汇总";
        if (number) number.textContent = payload.partnerNumber || "班组财务总览";

        financeDialog.querySelectorAll("[data-crew-finance-metric]").forEach((target) => {
            const numericValue = Number(valueAt(payload, target.dataset.crewFinanceMetric)) || 0;
            target.textContent = formatMoney(numericValue);
            target.classList.toggle("is-zero", numericValue === 0);
        });

        financeDialog.querySelectorAll("[data-crew-finance-chart]").forEach((chart) => {
            const source = chart.dataset.crewFinanceChart;
            const targetAmount = Number(valueAt(payload, chart.dataset.targetPath)) || 0;
            const completedAmount = Number(valueAt(payload, chart.dataset.completedPath)) || 0;
            const remainingAmount = Number(valueAt(payload, chart.dataset.remainingPath)) || 0;
            const rawPercentage = targetAmount > 0
                ? Math.max(completedAmount / targetAmount * 100, 0)
                : 0;
            const normalizedPercentage = targetAmount > 0
                ? Math.min(rawPercentage, 100)
                : completedAmount > 0 ? 100 : 0;
            const progressState = progressStateFor(targetAmount, completedAmount);
            const percentageText = targetAmount <= 0
                ? (completedAmount > 0 ? "超额" : "—")
                : `${Number(rawPercentage.toFixed(1))}%${rawPercentage > 100 ? " · 超额" : ""}`;
            const track = chart.querySelector("[role='progressbar']");
            const fill = chart.querySelector("[data-crew-finance-chart-fill]");
            const percent = chart.querySelector("[data-crew-finance-chart-percent]");
            const target = chart.querySelector("[data-crew-finance-chart-target]");
            const completed = chart.querySelector("[data-crew-finance-chart-completed]");
            const remaining = chart.querySelector("[data-crew-finance-chart-remaining]");

            chart.dataset.progressState = progressState;
            financeDialog.querySelectorAll("[data-crew-finance-state-source]").forEach((stateTarget) => {
                if (stateTarget.dataset.crewFinanceStateSource === source) {
                    stateTarget.dataset.progressState = progressState;
                }
            });
            if (fill) fill.style.width = `${normalizedPercentage}%`;
            if (percent) percent.textContent = percentageText;
            if (target) target.textContent = formatMoney(targetAmount);
            if (completed) completed.textContent = formatMoney(completedAmount);
            if (remaining) remaining.textContent = formatMoney(remainingAmount);
            target?.classList.toggle("is-zero", targetAmount === 0);
            completed?.classList.toggle("is-zero", completedAmount === 0);
            remaining?.classList.toggle("is-zero", remainingAmount === 0);
            if (track) {
                track.setAttribute("aria-valuenow", String(Number(normalizedPercentage.toFixed(1))));
                track.setAttribute("aria-valuetext", `${percentageText}，已完成 ${formatMoney(completedAmount)}，目标 ${formatMoney(targetAmount)}`);
            }
        });

        const jump = financeDialog.querySelector("[data-crew-finance-jump]");
        if (jump && payload.financeUrl) jump.href = payload.financeUrl;
        show(financeDialog);
    };

    page.querySelectorAll("[data-crew-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            const mode = trigger.dataset.crewDialogOpen;
            const payload = payloadFrom(trigger);
            if (mode === "details") openDetails(payload);
            else if (mode === "roster") void openRoster(payload);
            else if (mode === "finance") openFinance(payload);
            else openEditor(mode, payload);
        });
    });
    page.querySelectorAll("[data-crew-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });
    });

    if (editorDialog?.dataset.dialogOpen === "true") {
        const title = editorDialog.querySelector("[data-crew-editor-title]");
        if (title) title.textContent = field("Id")?.value ? "编辑施工班组" : "新增施工班组";
        if (statusSection) statusSection.hidden = !field("Id")?.value;
        show(editorDialog);
    }
}
