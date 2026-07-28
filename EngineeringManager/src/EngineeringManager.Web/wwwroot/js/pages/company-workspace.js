import { initAttachmentPreview } from "../components/attachment-preview.js";

const editDialog = document.querySelector("[data-company-workspace-dialog]");
const viewDialog = document.querySelector("[data-company-view-dialog]");

const value = (name, next) => {
  const field = editDialog?.querySelector(`[name="CompanyInput.${name}"]`);
  if (field) field.value = next ?? "";
};

const parse = (button) => JSON.parse(button.dataset.companyPayload || "{}");

const openEditor = (button, copy) => {
  const item = parse(button);
  value("Id", copy ? "" : item.Id);
  value("ConcurrencyStamp", copy ? "" : item.ConcurrencyStamp);
  value("Code", copy ? "" : item.Code);
  value("Name", copy ? `${item.Name} - 副本` : item.Name);
  value("ShortName", copy ? `${item.ShortName}副本` : item.ShortName);
  value("CompanyCategoryId", item.CompanyCategoryId);
  value("IsActive", copy ? "true" : String(item.IsActive).toLowerCase());
  value("LegalRepresentative", item.LegalRepresentative);
  value("UnifiedSocialCreditCode", copy ? "" : item.UnifiedSocialCreditCode);
  value("RegisteredAddress", item.RegisteredAddress);
  value("BusinessAddress", item.BusinessAddress);
  value("Phone", item.Phone);
  value("InvoiceTitle", copy ? `${item.Name} - 副本` : item.InvoiceTitle);
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
    ["基本资料", [["公司编码", item.Code], ["公司名称", item.Name], ["简称", item.ShortName], ["组合分类", item.CompanyCategoryName], ["状态", item.IsActive ? "启用" : "停用"]]],
    ["工商与联系资料", [["法人/经营者", item.LegalRepresentative], ["统一社会信用代码", item.UnifiedSocialCreditCode], ["注册地址", item.RegisteredAddress], ["经营地址", item.BusinessAddress], ["电话", item.Phone]]],
    ["开票与备注", [["开票抬头", item.InvoiceTitle], ["备注", item.Notes]], true]
  ];
  const content = viewDialog?.querySelector("[data-company-view-content]");
  if (content) {
    content.replaceChildren(...sections.map(([title, rows, wide]) => {
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
    }));
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
