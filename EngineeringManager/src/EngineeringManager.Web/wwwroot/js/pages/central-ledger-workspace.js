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
    const recordLabel = (type) => ({ Settlement: "结算", Invoice: "发票", Cash: "资金", Deduction: "扣款", Adjustment: "调整" }[type] || type);
    const sourceLabel = (type) => ({ CentralLedger: "中央账本直接录入", ProjectQuantity: "项目工程量", Crew: "施工班组", Partner: "合作商", ProjectCollection: "项目收款", LegacyMigration: "历史迁移" }[type] || type || "未知来源");

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
        const fields = [
            ["记录类型", recordLabel(get(details, "recordType", "RecordType"))],
            ["记录 ID", get(details, "id", "Id")],
            ["账本范围", get(details, "scope", "Scope") === "Internal" || get(details, "scope", "Scope") === 2 ? "内部账本" : "外部账本"],
            ["方向", get(details, "direction", "Direction") === "Payable" || get(details, "direction", "Direction") === 2 ? "应付/付款" : "应收/收款"],
            ["业务日期", formatDate(get(header, "businessDate", "BusinessDate", "invoiceDate", "InvoiceDate"))],
            ["自有公司", get(header, "legalEntity", "LegalEntity")],
            ["往来单位", get(header, "businessPartner", "BusinessPartner") || get(header, "counterLegalEntity", "CounterLegalEntity")],
            ["项目", get(header, "project", "Project")],
            ["合同", get(header, "contract", "Contract")],
            ["状态", get(header, "status", "Status", "settlementState", "SettlementState")],
            ["来源", sourceLabel(get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType"))],
            ["备注", get(header, "notes", "Notes")]
        ];
        if (!basicFields) return;
        basicFields.innerHTML = fields.map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("");
    };

    const renderMetrics = (details, header) => {
        if (!metrics) return;
        const metric = get(details, "metrics", "Metrics") || {};
        const recordType = get(details, "recordType", "RecordType");
        const items = recordType === "Settlement" ? [
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
            ? list.map(item => `<tr><td>${escapeHtml(get(item, "settlementId", "SettlementId"))}</td><td>${escapeHtml(get(item, "projectId", "ProjectId"))}</td><td>${escapeHtml(get(item, "contractId", "ContractId"))}</td><td class="numeric-cell"><strong class="ledger-amount ledger-amount--allocated">${formatMoney(get(item, "amount", "Amount"))}</strong></td><td>${escapeHtml(get(item, "allocationOrder", "AllocationOrder"))}</td></tr>`).join("")
            : `<tr><td colspan="5"><div class="empty-state"><strong>暂无分摊</strong><p>可以先保存记录，后续在此业务区完成部分或全部分摊。</p></div></td></tr>`;
    };

    const renderSource = (details, header) => {
        if (!source) return;
        const sourceType = get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType");
        const sourceUrl = get(details, "sourceUrl", "SourceUrl") || get(header, "sourceUrl", "SourceUrl");
        const sourceId = get(details, "sourceId", "SourceId") || get(header, "sourceId", "SourceId");
        const direct = !sourceType || sourceType === "CentralLedger" || sourceType === 4;
        const links = [];
        if (!direct && sourceUrl) links.push(`<a class="button button--secondary button--small" href="${escapeHtml(sourceUrl)}">打开来源模块</a>`);
        if (direct && currentRecord) links.push(`<a class="button button--secondary button--small" href="/Ledger/Entries/Edit?scope=${encodeURIComponent(scope)}&recordType=${encodeURIComponent(currentRecord.type)}&recordId=${encodeURIComponent(currentRecord.id)}&view=${encodeURIComponent(activeTab)}">快捷编辑</a>`);
        source.innerHTML = `<div class="ledger-source-summary"><span>${escapeHtml(direct ? "中央账本直接录入" : sourceLabel(sourceType))}</span><strong>${escapeHtml(sourceId || "无来源编号")}</strong></div><div class="ledger-source-actions">${links.join("")}</div>`;
    };

    const openDetails = async (trigger) => {
        if (!detailsDialog) return;
        currentRecord = { type: trigger.dataset.ledgerRecordType, id: trigger.dataset.ledgerRecordId, stamp: trigger.dataset.ledgerRecordStamp };
        detailsDialog.showModal();
        setBusy(true);
        if (title) title.textContent = `${recordLabel(currentRecord.type)}详情`;
        if (subtitle) subtitle.textContent = `记录 ${currentRecord.id}`;
        if (editButton) editButton.hidden = true;
        if (deleteButton) deleteButton.hidden = true;
        try {
            const url = new URL(trigger.dataset.ledgerDetailsUrl, window.location.origin);
            url.searchParams.set("type", currentRecord.type);
            url.searchParams.set("id", currentRecord.id);
            const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (!response.ok) throw new Error("详情加载失败");
            const details = await response.json();
            const header = parseHeader(details);
            renderBasic(details, header);
            renderMetrics(details, header);
            renderAllocations(details);
            renderSource(details, header);
            const sourceType = get(details, "sourceType", "SourceType") || get(header, "sourceType", "SourceType");
            const allocationList = get(details, "allocations", "Allocations") || [];
            const allocated = Number(get(header, "allocatedAmount", "AllocatedAmount") || 0);
            const direct = !sourceType || sourceType === "CentralLedger" || sourceType === 4;
            const canEdit = direct && (currentRecord.type === "Deduction" || allocationList.length === 0 && allocated === 0);
            if (editButton && canEdit) {
                editButton.hidden = false;
                editButton.href = `/Ledger/Entries/Edit?scope=${encodeURIComponent(scope)}&recordType=${encodeURIComponent(currentRecord.type)}&recordId=${encodeURIComponent(currentRecord.id)}&view=${encodeURIComponent(activeTab)}`;
            }
            if (deleteButton && direct && (currentRecord.type === "Deduction" || allocationList.length === 0 && allocated === 0)) deleteButton.hidden = false;
            setBusy(false);
        } catch (error) {
            if (loading) loading.innerHTML = `<div class="empty-state"><strong>详情加载失败</strong><p>${escapeHtml(error.message || "请刷新后重试")}</p></div>`;
            if (content) content.hidden = true;
        }
    };

    workspace.querySelectorAll("[data-ledger-details-open]").forEach(trigger => trigger.addEventListener("click", () => openDetails(trigger)));
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
