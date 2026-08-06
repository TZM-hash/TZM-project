const page = document.querySelector("[data-partner-workspace]");

if (page) {
    const editorDialog = page.querySelector("[data-partner-editor-dialog]");
    const detailsDialog = page.querySelector("[data-partner-details-dialog]");
    const financeDialog = page.querySelector("[data-partner-finance-dialog]");
    const editorForm = editorDialog?.querySelector("[data-partner-editor-form]");
    const statusSection = editorDialog?.querySelector("[data-partner-status-section]");
    const nextPartnerNumber = page.dataset.nextPartnerNumber || "";
    const defaultRole = Number.parseInt(page.dataset.defaultRole ?? "2", 10);
    const entityLabel = page.dataset.entityLabel || "合作单位";
    const money = new Intl.NumberFormat("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    const show = (dialog) => {
        if (dialog && !dialog.open) dialog.showModal();
    };
    const payloadFrom = (trigger) => JSON.parse(trigger.dataset.partnerPayload || "{}");
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
        setField("RoleType", payload.roleType ?? defaultRole);
        setField("PreviousRoleType", editing ? (payload.previousRoleType ?? payload.roleType ?? "") : "");
        setField("TradeCategory", payload.tradeCategory);
        setField("PricingRule", payload.pricingRule);
        setField("SettlementTerms", payload.settlementTerms);
        setField("ContactName", copy ? "" : payload.contactName);
        setField("ContactPhone", copy ? "" : payload.contactPhone);
        setField("ContactEmail", copy ? "" : payload.contactEmail);
        setField("ContactAddress", copy ? "" : payload.contactAddress);
        setField("ContactNotes", copy ? "" : payload.contactNotes);
        setField("Notes", payload.notes);
        setField("IsActive", editing ? payload.isActive : true);
        const action = copy ? "复制" : editing ? "编辑" : "新增";
        setField("Reason", `${action}${entityLabel}`);
        const title = editorDialog?.querySelector("[data-partner-editor-title]");
        if (title) title.textContent = `${action}${entityLabel}`;
        show(editorDialog);
    };

    const openDetails = (payload) => {
        setDetail("[data-partner-detail-title]", payload.name);
        setDetail("[data-partner-detail-number]", payload.partnerNumber);
        setDetail("[data-partner-detail-short-name]", payload.shortName);
        setDetail("[data-partner-detail-credit-code]", payload.unifiedSocialCreditCode);
        setDetail("[data-partner-detail-role]", payload.roleLabel);
        setDetail("[data-partner-detail-trade]", payload.tradeCategory);
        setDetail("[data-partner-detail-contact]", payload.contactName);
        setDetail("[data-partner-detail-phone]", payload.contactPhone);
        setDetail("[data-partner-detail-projects]", String(payload.projectCount ?? 0));
        setDetail("[data-partner-detail-status]", payload.statusLabel);
        setDetail("[data-partner-detail-notes]", payload.notes);
        setDetail("[data-partner-detail-contact-notes]", payload.contactNotes);
        const finance = detailsDialog?.querySelector("[data-partner-detail-finance]");
        if (finance) finance.href = payload.financeUrl || "";
        show(detailsDialog);
    };

    const openFinance = (payload) => {
        if (!financeDialog) return;

        const title = financeDialog.querySelector("[data-partner-finance-title]");
        const number = financeDialog.querySelector("[data-partner-finance-number]");
        if (title) title.textContent = payload.name || "合作单位财务汇总";
        if (number) number.textContent = payload.partnerNumber || "单位财务总览";

        financeDialog.querySelectorAll("[data-partner-finance-metric]").forEach((target) => {
            const numericValue = Number(valueAt(payload, target.dataset.partnerFinanceMetric)) || 0;
            target.textContent = formatMoney(numericValue);
            target.classList.toggle("is-zero", numericValue === 0);
        });

        financeDialog.querySelectorAll("[data-partner-finance-chart]").forEach((chart) => {
            const source = chart.dataset.partnerFinanceChart;
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
            const fill = chart.querySelector("[data-partner-finance-chart-fill]");
            const percent = chart.querySelector("[data-partner-finance-chart-percent]");
            const target = chart.querySelector("[data-partner-finance-chart-target]");
            const completed = chart.querySelector("[data-partner-finance-chart-completed]");
            const remaining = chart.querySelector("[data-partner-finance-chart-remaining]");

            chart.dataset.progressState = progressState;
            financeDialog.querySelectorAll("[data-partner-finance-state-source]").forEach((target) => {
                if (target.dataset.partnerFinanceStateSource === source) {
                    target.dataset.progressState = progressState;
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

        const jump = financeDialog.querySelector("[data-partner-finance-jump]");
        if (jump && payload.financeUrl) jump.href = payload.financeUrl;
        show(financeDialog);
    };

    page.querySelectorAll("[data-partner-dialog-open]").forEach((trigger) => {
        trigger.addEventListener("click", () => {
            const mode = trigger.dataset.partnerDialogOpen;
            const payload = payloadFrom(trigger);
            if (mode === "details") openDetails(payload);
            else if (mode === "finance") openFinance(payload);
            else openEditor(mode, payload);
        });
    });
    page.querySelectorAll("[data-partner-dialog-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest("dialog")?.close());
    });
    page.querySelectorAll("dialog").forEach((dialog) => {
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });
    });

    if (editorDialog?.dataset.dialogOpen === "true") {
        const title = editorDialog.querySelector("[data-partner-editor-title]");
        if (title) title.textContent = `${field("Id")?.value ? "编辑" : "新增"}${entityLabel}`;
        if (statusSection) statusSection.hidden = !field("Id")?.value;
        show(editorDialog);
    }
}
