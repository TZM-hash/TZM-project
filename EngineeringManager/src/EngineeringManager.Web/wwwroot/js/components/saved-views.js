import { getWorkbenchTableState } from "./data-table.js";

function safeParse(value) {
  try { return JSON.parse(value || "{}") || {}; } catch { return {}; }
}

function applySavedView(root, option) {
  const url = new URL(window.location.href);
  if (!option?.value) {
    url.searchParams.delete("savedViewId");
    url.searchParams.delete("page");
    window.location.assign(url);
    return;
  }
  const filters = safeParse(option.dataset.savedViewFilterJson);
  root.querySelectorAll("[data-filter-form] [data-filter-key], [data-inline-filter-key]").forEach((control) => url.searchParams.delete(control.dataset.filterKey || control.dataset.inlineFilterKey));
  Object.entries(filters).forEach(([key, value]) => {
    if (Array.isArray(value)) {
      url.searchParams.delete(key);
      value.filter(Boolean).forEach((item) => url.searchParams.append(key, item));
    } else if (value !== null && value !== undefined && String(value).length) {
      url.searchParams.set(key, String(value));
    }
  });
  if (option.dataset.savedViewSortKey) url.searchParams.set("sort", option.dataset.savedViewSortKey);
  url.searchParams.set("descending", option.dataset.savedViewSortDescending || "false");
  url.searchParams.set("pageSize", option.dataset.savedViewPageSize || "20");
  url.searchParams.set("savedViewId", option.value);
  url.searchParams.delete("page");
  window.location.assign(url);
}

function serializeFilters(root) {
  const result = {};
  root.querySelectorAll("[data-filter-form] [data-filter-key], [data-inline-filter-key]").forEach((control) => {
    const key = control.dataset.filterKey || control.dataset.inlineFilterKey;
    if (!control.value) return;
    if (result[key]) {
      result[key] = [result[key], control.value].flat();
    } else {
      result[key] = control.value;
    }
  });
  return result;
}

function prepareSave(root, form) {
  const state = getWorkbenchTableState(root);
  form.querySelector("[data-saved-view-filter-json]").value = JSON.stringify(serializeFilters(root));
  form.querySelector("[data-saved-view-column-json]").value = JSON.stringify(state.columns);
  form.querySelector("[data-saved-view-row-density]").value = state.density;
  form.querySelector("[data-saved-view-page-size]").value = String(state.pageSize);
}

export function initSavedViews() {
  document.querySelectorAll("[data-workbench]").forEach((root) => {
    const dialog = root.querySelector("[data-save-view-dialog]");
    const form = root.querySelector("[data-save-view-form]");
    root.querySelector("[data-saved-view-select]")?.addEventListener("change", (event) => applySavedView(root, event.target.selectedOptions[0]));
    root.querySelector("[data-open-save-view]")?.addEventListener("click", () => {
      if (form) prepareSave(root, form);
      dialog?.showModal();
    });
    form?.addEventListener("submit", () => prepareSave(root, form));
    root.querySelectorAll("[data-delete-saved-view]").forEach((button) => button.addEventListener("click", (event) => {
      if (!window.confirm("确定删除这个个人视图吗？")) event.preventDefault();
    }));
  });
}
