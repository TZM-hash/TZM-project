import { initAttachmentPreview } from "../components/attachment-preview.js";

const editDialog = document.querySelector("[data-company-workspace-dialog]");
const viewDialog = document.querySelector("[data-company-view-dialog]");

const value = (name, next) => {
  const field = editDialog?.querySelector(`[name="CompanyInput.${name}"]`);
  if (field) field.value = next ?? "";
};

const parse = (button) => JSON.parse(button.dataset.companyPayload || "{}");
const formatAmount = (amount) => new Intl.NumberFormat("zh-CN", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
}).format(Number(amount ?? 0));
const formatCount = (count) => String(count ?? 0);
const toFiniteAmount = (amount) => {
  const value = Number(amount ?? 0);
  return Number.isFinite(value) ? value : 0;
};
const formatRate = (completed, total) => {
  const totalAmount = toFiniteAmount(total);
  if (totalAmount <= 0) return "0.0%";
  const rate = Math.min(Math.max(toFiniteAmount(completed) / totalAmount * 100, 0), 100);
  return `${rate.toFixed(1)}%`;
};

const createDetailSection = (title, rows, wide = false) => {
  const section = document.createElement("section");
  section.className = `entity-detail-section${wide ? " entity-detail-section--wide" : ""}`;
  const heading = document.createElement("h3");
  heading.className = "entity-detail-section-heading";
  heading.textContent = title;
  const grid = document.createElement("dl");
  grid.className = "entity-detail-field-grid";
  grid.append(...rows.map(([label, text]) => {
    const row = document.createElement("div");
    const term = document.createElement("dt");
    const description = document.createElement("dd");
    term.textContent = label;
    description.textContent = text || "未填写";
    row.append(term, description);
    return row;
  }));
  section.append(heading, grid);
  return section;
};

const createSummaryMetric = ({ label, value, href, variant }) => {
  const metric = document.createElement(href ? "a" : "div");
  metric.className = `organization-summary__metric${variant ? ` organization-summary__metric--${variant}` : ""} company-view-summary-metric`;
  if (href) metric.href = href;
  const name = document.createElement("span");
  name.textContent = label;
  const amount = document.createElement("strong");
  amount.textContent = value;
  metric.append(name, amount);
  return metric;
};

const createSummaryGroup = ({ title, metrics, className = "" }) => {
  const group = document.createElement("div");
  group.className = `company-view-summary-group${className ? ` ${className}` : ""}`;
  const groupHeading = document.createElement("h4");
  groupHeading.textContent = title;
  const metricGrid = document.createElement("div");
  metricGrid.className = "company-view-summary-metrics";
  metricGrid.append(...metrics.map(createSummaryMetric));
  group.append(groupHeading, metricGrid);
  return group;
};

const createSummarySection = (title, groups) => {
  const section = document.createElement("section");
  section.className = "entity-detail-section entity-detail-section--wide company-view-summary-section";
  const heading = document.createElement("h3");
  heading.className = "entity-detail-section-heading";
  heading.textContent = title;
  const groupGrid = document.createElement("div");
  groupGrid.className = "company-view-summary-groups";
  groupGrid.append(...groups.map(createSummaryGroup));
  section.append(heading, groupGrid);
  return section;
};

const createInvoiceMetrics = (summary) => [
  { label: "应开", value: formatAmount(summary.RequiredAmount), variant: "invoice" },
  { label: "已开", value: formatAmount(summary.IssuedAmount), variant: "invoice" },
  { label: "未开", value: formatAmount(summary.UnissuedAmount), variant: "invoice" }
];

const createFinanceSummarySection = (finance) => {
  const section = document.createElement("section");
  section.className = "entity-detail-section entity-detail-section--wide company-view-summary-section company-view-finance-section";
  const heading = document.createElement("h3");
  heading.className = "entity-detail-section-heading";
  heading.textContent = "经营财务汇总";

  const overviewGroups = document.createElement("div");
  overviewGroups.className = "company-view-finance-groups";
  overviewGroups.append(
    createSummaryGroup({
      title: "收款",
      className: "company-view-finance-row company-view-finance-row--cash",
      metrics: [
        { label: "应收", value: formatAmount(finance.ReceivableAmount), variant: "cash" },
        { label: "已收", value: formatAmount(finance.CollectedAmount), variant: "cash" },
        { label: "未收", value: formatAmount(finance.UncollectedAmount), variant: "cash" },
        { label: "收款率", value: formatRate(finance.CollectedAmount, finance.ReceivableAmount), variant: "cash" }
      ]
    }),
    createSummaryGroup({
      title: "付款",
      className: "company-view-finance-row company-view-finance-row--payment",
      metrics: [
        { label: "应付", value: formatAmount(finance.PayableAmount), variant: "payment" },
        { label: "已付", value: formatAmount(finance.PaidAmount), variant: "payment" },
        { label: "未付", value: formatAmount(finance.UnpaidAmount), variant: "payment" },
        { label: "付款率", value: formatRate(finance.PaidAmount, finance.PayableAmount), variant: "payment" }
      ]
    }),
    createSummaryGroup({
      title: "工程金额",
      className: "company-view-finance-row company-view-finance-row--engineering",
      metrics: [
        { label: "合同金额", value: formatAmount(finance.ContractAmount), variant: "finance" },
        { label: "当前工程金额", value: formatAmount(finance.CurrentEngineeringAmount), variant: "finance" }
      ]
    })
  );

  const invoiceGroup = document.createElement("div");
  invoiceGroup.className = "company-view-finance-invoice-group";
  const invoiceHeading = document.createElement("h4");
  invoiceHeading.className = "company-view-finance-invoice-heading";
  invoiceHeading.textContent = "开票";
  const invoiceDirections = document.createElement("div");
  invoiceDirections.className = "company-view-finance-invoice-directions";
  invoiceDirections.append(
    createSummaryGroup({
      title: "销项发票",
      metrics: createInvoiceMetrics(finance.OutputInvoice),
      className: "company-view-invoice-direction"
    }),
    createSummaryGroup({
      title: "进项发票",
      metrics: createInvoiceMetrics(finance.InputInvoice),
      className: "company-view-invoice-direction"
    })
  );
  invoiceGroup.append(invoiceHeading, invoiceDirections);
  section.append(heading, overviewGroups, invoiceGroup);
  return section;
};

const openEditor = (button, copy) => {
  const item = parse(button);
  value("Id", copy ? "" : item.Id);
  value("ConcurrencyStamp", copy ? "" : item.ConcurrencyStamp);
  value("Code", copy ? "" : item.Code);
  value("Name", copy ? `${item.Name}（副本）` : item.Name);
  value("ShortName", copy ? `${item.ShortName}副本` : item.ShortName);
  value("CompanyCategoryId", item.CompanyCategoryId);
  value("IsActive", copy ? "true" : String(item.IsActive).toLowerCase());
  value("LegalRepresentative", item.LegalRepresentative);
  value("UnifiedSocialCreditCode", copy ? "" : item.UnifiedSocialCreditCode);
  value("RegisteredAddress", item.RegisteredAddress);
  value("BusinessAddress", item.BusinessAddress);
  value("Phone", item.Phone);
  value("InvoiceTitle", copy ? `${item.Name}（副本）` : item.InvoiceTitle);
  value("Notes", item.Notes);
  value("Reason", copy ? "复制公司档案" : "修改公司档案");
  const title = editDialog?.querySelector("[data-company-dialog-title]");
  if (title) title.textContent = copy ? "复制公司" : "编辑公司";
  editDialog?.showModal();
};

document.querySelectorAll("[data-company-edit-open]").forEach((button) => button.addEventListener("click", () => openEditor(button, false)));
document.querySelectorAll("[data-company-copy-open]").forEach((button) => button.addEventListener("click", () => openEditor(button, true)));
editDialog?.querySelectorAll("[data-company-dialog-close]").forEach((button) => button.addEventListener("click", () => editDialog.close()));
if (editDialog?.dataset.dialogOpen === "true") editDialog.showModal();

document.querySelectorAll("[data-company-view-open]").forEach((button) => button.addEventListener("click", () => {
  const item = parse(button);
  const sections = [
    createDetailSection("基本资料", [["公司编码", item.Code], ["公司名称", item.Name], ["简称", item.ShortName], ["组合分类", item.CompanyCategoryName], ["状态", item.IsActive ? "启用" : "停用"]]),
    createDetailSection("工商与联系资料", [["法人/经营者", item.LegalRepresentative], ["统一社会信用代码", item.UnifiedSocialCreditCode], ["注册地址", item.RegisteredAddress], ["经营地址", item.BusinessAddress], ["电话", item.Phone]]),
    createDetailSection("开票与备注", [["开票抬头", item.InvoiceTitle], ["备注", item.Notes]], true)
  ];
  const organization = item.OrganizationSummary;
  if (organization) {
    sections.push(createSummarySection(`组织汇总${organization.AsOf ? ` · ${organization.AsOf}` : ""}`, [
      {
        title: "项目状态",
        metrics: [
          { label: "项目总数", value: formatCount(organization.Projects.TotalCount), href: organization.Projects.Links.Total, variant: "project" },
          { label: "进行中", value: formatCount(organization.Projects.InProgressCount), href: organization.Projects.Links.InProgress, variant: "project" },
          { label: "停工中", value: formatCount(organization.Projects.SuspendedCount), href: organization.Projects.Links.Suspended, variant: "project" },
          { label: "已完工未结算", value: formatCount(organization.Projects.CompletedUnsettledCount), href: organization.Projects.Links.CompletedUnsettled, variant: "project" },
          { label: "部分结算", value: formatCount(organization.Projects.PartiallySettledCount), href: organization.Projects.Links.PartiallySettled, variant: "project" },
          { label: "已结算归档", value: formatCount(organization.Projects.SettledArchivedCount), href: organization.Projects.Links.SettledArchived, variant: "project" }
        ]
      },
      {
        title: "人员状态",
        metrics: [
          { label: "当前人员", value: formatCount(organization.Personnel.TotalCurrentCount), href: organization.Personnel.Links.All, variant: "personnel" },
          { label: "启用人员", value: formatCount(organization.Personnel.ActiveCount), href: organization.Personnel.Links.Active, variant: "personnel" },
          { label: "正式员工", value: formatCount(organization.Personnel.FormalCount), href: organization.Personnel.Links.Formal, variant: "personnel" },
          { label: "劳务员工", value: formatCount(organization.Personnel.LaborCount), href: organization.Personnel.Links.Labor, variant: "personnel" },
          { label: "特殊临时人员", value: formatCount(organization.Personnel.TemporaryCount), href: organization.Personnel.Links.Temporary, variant: "personnel" }
        ]
      },
      {
        title: "部门",
        metrics: [{ label: "启用 / 总部门", value: `${formatCount(organization.Departments.ActiveCount)} / ${formatCount(organization.Departments.TotalCount)}`, href: organization.Departments.Link, variant: "department" }]
      }
    ]));
  }
  const finance = item.FinanceSummary;
  if (finance) {
    sections.push(createFinanceSummarySection(finance));
  }
  const content = viewDialog?.querySelector("[data-company-view-content]");
  if (content) {
    content.replaceChildren(...sections);
  }
  viewDialog?.showModal();
}));
viewDialog?.querySelectorAll("[data-company-view-close]").forEach((button) => button.addEventListener("click", () => viewDialog.close()));

document.querySelectorAll("[data-company-certificates-open]").forEach((button) => button.addEventListener("click", () => {
  document.querySelector(`[data-company-certificates-dialog="${button.dataset.companyId}"]`)?.showModal();
}));
document.querySelectorAll("[data-company-certificates-dialog]").forEach((dialog) => {
  dialog.querySelectorAll("[data-company-certificates-close]").forEach((button) => button.addEventListener("click", () => dialog.close()));
});

initAttachmentPreview();
