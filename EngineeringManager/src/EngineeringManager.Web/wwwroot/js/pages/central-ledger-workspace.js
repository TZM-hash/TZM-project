const workspace = document.querySelector("[data-ledger-workspace]");

if (workspace) {
    const scope = workspace.dataset.ledgerScope || "External";
    const activeTab = workspace.dataset.ledgerActiveTab || "overview";
    const detailsDialog = document.querySelector("[data-ledger-details-dialog]");
    const deleteDialog = document.querySelector("[data-ledger-delete-dialog]");
    const loading = detailsDialog?.querySelector("[data-ledger-details-loading]");
    const content = detailsDialog?.querySelector("[data-ledger-details-content]");
    const title = detailsDialog?.querySelector("[data-ledger-details-title]");
    const subtitle = detailsDialog?.querySelector("[data-ledger-details-subtitle]");
    const basicFields = detailsDialog?.querySelector("[data-ledger-basic-fields]");
    const metrics = detailsDialog?.querySelector("[data-ledger-metrics]");
    const allocations = detailsDialog?.querySelector("[data-ledger-allocations]");
    const source = detailsDialog?.querySelector("[data-ledger-source]");
    const editButton = detailsDialog?.querySelector("[data-ledger-detail-edit]");
    const deleteButton = detailsDialog?.querySelector("[data-ledger-delete-open]");
    let currentRecord = null;

    const get = (object, ...keys) => {
        for (const key of keys) {
            if (object && object[key] !== undefined && object[key] !== null) return object[key];
        }
        return null;
    };

    const escapeHtml = (value) => String(value ?? "-")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");

    const formatMoney = (value) => Number(value || 0).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formatDate = (value) => {
        if (!value) return "-";
        const date = String(value).slice(0, 10);
        return date === "0001-01-01" ? "日期待确认" : date;
    };
    const enumLabel = (value, labels, fallback = "-") => {
        if (value === null || value === undefined || value === "") return fallback;
        const raw = String(value);
        if (labels[raw]) return labels[raw];
        const key = Object.keys(labels).find(item => item.toLowerCase() === raw.toLowerCase());
        return key ? labels[key] : raw;
    };
    const recordLabel = (type) => enumLabel(type, {
        "1": "结算", Settlement: "结算",
        "2": "扣款", Deduction: "扣款",
        "3": "发票", Invoice: "发票",
        "4": "资金", Cash: "资金",
        "5": "调整", Adjustment: "调整"
    }, "未知记录");
    const sourceLabel = (type) => enumLabel(type, {
        "1": "项目工程量", ProjectQuantity: "项目工程量",
        "2": "施工班组", Crew: "施工班组",
        "3": "合作单位", Partner: "合作单位",
        "4": "中央账本直接录入", CentralLedger: "中央账本直接录入",
        "5": "历史迁移", LegacyMigration: "历史迁移",
        "6": "项目收款", ProjectCollection: "项目收款"
    }, "未知来源");
    const scopeLabel = (value) => enumLabel(value, { "1": "外部账本", External: "外部账本", "2": "内部账本", Internal: "内部账本" });
    const directionLabel = (value) => enumLabel(value, { "1": "应收/收款", Receivable: "应收/收款", "2": "应付/付款", Payable: "应付/付款" });
    const settlementStateLabel = (value) => enumLabel(value, { "1": "暂估", Provisional: "暂估", "2": "正式", Final: "正式" });
    const recordStatusLabel = (value) => enumLabel(value, { "1": "有效", Active: "有效", "2": "已作废", Voided: "已作废" });
    const isEnum = (value, name, number) => String(value).toLowerCase() === name.toLowerCase() || Number(value) === number;
    const referenceLabel = (number, name, fallback) => {
        const parts = [number, name].filter(value => value !== null && value !== undefined && String(value).trim() !== "");
        return parts.length ? parts.join(" · ") : fallback;
    };

    const parseHeader = (details) => {
        const raw = get(details, "headerJson", "HeaderJson");
        if (!raw) return {};
        if (typeof raw === "object") return raw;
        try { return JSON.parse(raw); } catch { return {}; }
    };

    const setBusy = (busy) => {
        if (loading) loading.hidden = !busy;
        if (content) content.hidden = busy;
    };

    const renderBasic = (details, header) => {
        const recordType = get(details, "recordType", "RecordType");
        const recordNumber = get(header, "invoiceNumber", "InvoiceNumber", "recordNumber", "RecordNumber") || `${recordLabel(recordType)}记录`;
        const status = get(header, "status", "Status");
        const settlementState = get(header, "settlementState", "SettlementState");
        const fields = [
            ["记录类型", recordLabel(recordType)],
            ["业务编号", recordNumber],
            ["账本范围", scopeLabel(get(details, "scope", "Scope"))],
            ["方向", directionLabel(get(details, "direction", "Direction"))],
            ["业务日期", formatDate(get(header, "businessDate", "BusinessDate", "invoiceDate", "InvoiceDate"))],
            ["自有公司", get(header, "legalEntity", "LegalEntity")],
            ["往来单位", get(header, "businessPartner", "BusinessPartner") || get(header, "counterLegalEntity", "CounterLegalEntity")],
            ["项目", referenceLabel(get(header, "projectNumber", "ProjectNumber"), get(header, "project", "Project"), "未关联项目")],
            ["合同", referenceLabel(get(header, "contractNumber", "ContractNumber"), get(header, "contract", "Contract"), "未关联合同")],
            ["状态", recordStatusLabel(status)],
            ["结算状态", settlementStateLabel(settlementState)],
            ["来源", sourceLabel(get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType"))],
            ["备注", get(header, "notes", "Notes")]
        ];
        if (!basicFields) return;
        basicFields.innerHTML = fields
            .filter(([, value]) => value !== null && value !== undefined && value !== "")
            .map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("");
    };

    const renderMetrics = (details, header) => {
        if (!metrics) return;
        const metric = get(details, "metrics", "Metrics") || {};
        const recordType = get(details, "recordType", "RecordType");
        const items = isEnum(recordType, "Settlement", 1) ? [
            ["结算金额", get(metric, "grossSettlementAmount", "GrossSettlementAmount"), "settlement"],
            ["扣款", get(metric, "deductions", "Deductions"), "deduction"],
            ["实际应收/应付", get(metric, "actualAmount", "ActualAmount"), "actual"],
            ["应开票", get(metric, "shouldInvoiceAmount", "ShouldInvoiceAmount"), "invoice"],
            ["已开票", get(metric, "invoicedAmount", "InvoicedAmount"), "invoice"],
            ["已收/已付", get(metric, "cashAmount", "CashAmount"), "paid"],
            ["未收/未付", get(metric, "uncollectedOrUnpaid", "UncollectedOrUnpaid"), "balance"],
            ["异常超额", Number(get(metric, "advanceInvoiceCash", "AdvanceInvoiceCash") || 0) + Number(get(metric, "overSettlementCash", "OverSettlementCash") || 0) + Number(get(metric, "overInvoiced", "OverInvoiced") || 0), "danger"]
        ] : [
            ["原始金额", get(header, "amount", "Amount", "originalAmount", "OriginalAmount"), "actual"],
            ["已分摊", get(header, "allocatedAmount", "AllocatedAmount"), "allocated"],
            ["待分摊", get(header, "unallocatedAmount", "UnallocatedAmount"), "pending"]
        ];
        metrics.innerHTML = items.map(([label, value, kind]) => `<article class="ledger-detail-metric ledger-detail-metric--${kind}"><span>${escapeHtml(label)}</span><strong>${formatMoney(value)}</strong></article>`).join("");
    };

    const renderAllocations = (details) => {
        if (!allocations) return;
        const list = get(details, "allocations", "Allocations") || [];
        allocations.innerHTML = list.length
            ? list.map(item => {
                const projectId = get(item, "projectId", "ProjectId");
                const projectText = referenceLabel(get(item, "projectNumber", "ProjectNumber"), get(item, "projectName", "ProjectName"), "未关联项目");
                const project = projectId && projectText !== "未关联项目"
                    ? `<a href="/Projects/Details/${encodeURIComponent(projectId)}">${escapeHtml(projectText)}</a>`
                    : escapeHtml(projectText);
                const contract = escapeHtml(referenceLabel(get(item, "contractNumber", "ContractNumber"), get(item, "contractName", "ContractName"), "未关联合同"));
                return `<tr><td>${escapeHtml(get(item, "settlementLabel", "SettlementLabel") || "结算记录")}</td><td>${project}</td><td>${contract}</td><td class="numeric-cell"><strong class="ledger-amount ledger-amount--allocated">${formatMoney(get(item, "amount", "Amount"))}</strong></td><td>${escapeHtml(get(item, "allocationOrder", "AllocationOrder"))}</td></tr>`;
            }).join("")
            : `<tr><td colspan="5"><div class="empty-state"><strong>暂无分摊</strong><p>可以先保存记录，后续在此业务区完成部分或全部分摊。</p></div></td></tr>`;
    };

    const renderSource = (details, header) => {
        if (!source) return;
        const sourceType = get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType");
        const sourceUrl = get(details, "sourceUrl", "SourceUrl") || get(header, "sourceUrl", "SourceUrl");
        const direct = !sourceType || isEnum(sourceType, "CentralLedger", 4);
        const sourceDisplay = sourceType ? sourceLabel(sourceType) : get(details, "sourceLabel", "SourceLabel") || "中央账本直接录入";
        const links = [];
        if (!direct && sourceUrl) links.push(`<a class="button button--secondary button--small" href="${escapeHtml(sourceUrl)}">打开来源模块</a>`);
        if (direct && currentRecord) links.push(`<a class="button button--secondary button--small" href="/Ledger/Entries/Edit?scope=${encodeURIComponent(scope)}&recordType=${encodeURIComponent(currentRecord.type)}&recordId=${encodeURIComponent(currentRecord.id)}&view=${encodeURIComponent(activeTab)}">快捷编辑</a>`);
        source.innerHTML = `<div class="ledger-source-summary"><span>来源类型</span><strong>${escapeHtml(sourceDisplay)}</strong></div><div class="ledger-source-actions">${links.join("")}</div>`;
    };

    const openDetails = async (trigger) => {
        if (!detailsDialog) return;
        currentRecord = { type: trigger.dataset.ledgerRecordType, id: trigger.dataset.ledgerRecordId, stamp: trigger.dataset.ledgerRecordStamp };
        detailsDialog.showModal();
        setBusy(true);
        if (title) title.textContent = `${recordLabel(currentRecord.type)}详情`;
        if (subtitle) subtitle.textContent = "正在加载可读详情…";
        if (editButton) {
            editButton.hidden = true;
            editButton.removeAttribute("href");
        }
        if (deleteButton) deleteButton.hidden = true;
        try {
            const url = new URL(trigger.dataset.ledgerDetailsUrl, window.location.origin);
            url.searchParams.set("type", currentRecord.type);
            url.searchParams.set("id", currentRecord.id);
            const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (!response.ok) throw new Error("详情加载失败");
            const details = await response.json();
            currentRecord.stamp = get(details, "concurrencyStamp", "ConcurrencyStamp") || currentRecord.stamp;
            const header = parseHeader(details);
            renderBasic(details, header);
            renderMetrics(details, header);
            renderAllocations(details);
            renderSource(details, header);
            const sourceType = get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType");
            const allocationList = get(details, "allocations", "Allocations") || [];
            const allocated = Number(get(header, "allocatedAmount", "AllocatedAmount") || 0);
            const direct = !sourceType || isEnum(sourceType, "CentralLedger", 4);
            const canEdit = direct && (isEnum(currentRecord.type, "Deduction", 2) || allocationList.length === 0 && allocated === 0);
            const editUrl = `/Ledger/Entries/Edit?scope=${encodeURIComponent(scope)}&recordType=${encodeURIComponent(currentRecord.type)}&recordId=${encodeURIComponent(currentRecord.id)}&view=${encodeURIComponent(activeTab)}`;
            if (subtitle) subtitle.textContent = `${get(header, "invoiceNumber", "InvoiceNumber", "recordNumber", "RecordNumber") || `${recordLabel(get(details, "recordType", "RecordType"))}记录`} · ${scopeLabel(get(details, "scope", "Scope"))}`;
            if (editButton) {
                editButton.href = editUrl;
                editButton.hidden = !canEdit;
            }
            if (deleteButton) deleteButton.hidden = !(direct && (isEnum(currentRecord.type, "Deduction", 2) || allocationList.length === 0 && allocated === 0));
            setBusy(false);
        } catch (error) {
            if (loading) loading.innerHTML = `<div class="empty-state"><strong>详情加载失败</strong><p>${escapeHtml(error.message || "请刷新后重试")}</p></div>`;
            if (content) content.hidden = true;
        }
    };

    workspace.querySelectorAll("[data-ledger-details-open]").forEach(trigger => trigger.addEventListener("click", () => openDetails(trigger)));
    workspace.querySelectorAll("[data-ledger-project-url]").forEach(row => {
        const navigateToProject = () => {
            const url = row.dataset.ledgerProjectUrl;
            if (url) window.location.assign(url);
        };
        row.addEventListener("click", event => {
            if (event.defaultPrevented || event.target.closest("a,button,input,select,textarea,summary")) return;
            navigateToProject();
        });
        row.addEventListener("keydown", event => {
            if (event.key !== "Enter" && event.key !== " ") return;
            event.preventDefault();
            navigateToProject();
        });
    });
    document.querySelectorAll("[data-ledger-dialog-close]").forEach(button => button.addEventListener("click", () => detailsDialog?.close()));
    document.querySelectorAll("[data-ledger-delete-close]").forEach(button => button.addEventListener("click", () => deleteDialog?.close()));
    deleteButton?.addEventListener("click", () => {
        if (!currentRecord || !deleteDialog) return;
        deleteDialog.querySelector("[data-ledger-delete-type]").value = currentRecord.type;
        deleteDialog.querySelector("[data-ledger-delete-id]").value = currentRecord.id;
        deleteDialog.querySelector("[data-ledger-delete-stamp]").value = currentRecord.stamp || "";
        detailsDialog?.close();
        deleteDialog.showModal();
    });
}
