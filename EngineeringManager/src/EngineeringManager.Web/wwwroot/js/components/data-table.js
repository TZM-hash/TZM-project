import { deleteSearchParamsIgnoreCase } from "./url-search-params.js";

const rowSpacingClasses = ["row-spacing-compact", "row-spacing-standard", "row-spacing-spacious"];

function safeParse(value, fallback) {
  try { return JSON.parse(value || "") ?? fallback; } catch { return fallback; }
}

function storageKey(root) {
  return `engineering-manager-workbench:${root.dataset.pageKey}:${root.dataset.tableId}`;
}

function tableFor(root) {
  return document.getElementById(root.dataset.tableId);
}

function readColumnState(root) {
  return Array.from(root.querySelectorAll("[data-column-list] [data-column-key][data-column-order]")).map((item, order) => ({
    key: item.dataset.columnKey,
    visible: item.dataset.columnFixed === "true" || item.querySelector("[data-column-visibility]")?.checked !== false,
    fixed: item.dataset.columnFixed === "true",
    order
  }));
}

function normalizeColumns(root, requested) {
  const defaults = readColumnState(root);
  const requestedMap = new Map((Array.isArray(requested) ? requested : []).map((item) => [item.key, item]));
  return defaults
    .map((item, fallbackOrder) => {
      const saved = requestedMap.get(item.key);
      return { ...item, visible: item.fixed || (saved?.visible ?? item.visible), order: Number.isInteger(saved?.order) ? saved.order : fallbackOrder };
    })
    .sort((left, right) => left.order - right.order)
    .map((item, order) => ({ ...item, order }));
}

function applyColumnControls(root, columns) {
  const list = root.querySelector("[data-column-list]");
  columns.forEach((column) => {
    const item = list?.querySelector(`[data-column-key="${CSS.escape(column.key)}"]`);
    if (!item) return;
    item.dataset.columnOrder = String(column.order);
    const checkbox = item.querySelector("[data-column-visibility]");
    if (checkbox && !checkbox.disabled) checkbox.checked = column.visible;
    list.appendChild(item);
  });
}

function applyColumns(root, columns) {
  const table = tableFor(root);
  if (!table) return;
  const byKey = new Map(columns.map((item) => [item.key, item]));
  table.querySelectorAll("tr").forEach((row) => {
    const cells = Array.from(row.children).filter((cell) => cell.dataset.columnKey);
    cells.sort((left, right) => (byKey.get(left.dataset.columnKey)?.order ?? 999) - (byKey.get(right.dataset.columnKey)?.order ?? 999));
    cells.forEach((cell) => {
      cell.hidden = byKey.get(cell.dataset.columnKey)?.visible === false;
      row.appendChild(cell);
    });
  });
  applyColumnControls(root, columns);
  const exportForm = document.getElementById(`${root.dataset.tableId}-export-form`) ?? root.querySelector("[data-project-export-scope]");
  const exportInputs = Array.from(exportForm?.querySelectorAll("[data-export-column-key]") ?? []);
  exportInputs.sort((left, right) => (byKey.get(left.dataset.exportColumnKey)?.order ?? 999) - (byKey.get(right.dataset.exportColumnKey)?.order ?? 999));
  exportInputs.forEach((input) => {
    input.disabled = byKey.get(input.dataset.exportColumnKey)?.visible === false;
    exportForm.appendChild(input);
  });
}

function applyRowSpacing(root, spacing) {
  const value = ["compact", "standard", "spacious"].includes(spacing) ? spacing : "standard";
  const table = tableFor(root);
  root.classList.remove(...rowSpacingClasses);
  root.classList.add(`row-spacing-${value}`);
  if (table) {
    table.classList.remove(...rowSpacingClasses);
    table.classList.add(`row-spacing-${value}`);
  }
  root.dataset.rowDensity = value;
  root.querySelectorAll("[data-row-spacing]").forEach((button) => button.setAttribute("aria-pressed", String(button.dataset.rowSpacing === value)));
}

function persist(root) {
  const state = {
    columns: readColumnState(root),
    density: root.dataset.rowDensity || "standard",
    pageSize: Number(root.querySelector("[data-current-page-size]")?.value || 20)
  };
  try { localStorage.setItem(storageKey(root), JSON.stringify(state)); } catch { /* storage unavailable */ }
  return state;
}

function readLocalState(root) {
  try { return safeParse(localStorage.getItem(storageKey(root)), {}); } catch { return {}; }
}

function initialState(root) {
  const serverColumns = safeParse(root.dataset.savedViewColumns, []);
  const local = readLocalState(root);
  const localColumns = Array.isArray(local.columns) && local.columns.length ? local.columns : null;
  const hasExplicitSavedView = Boolean(root.dataset.currentSavedViewId);
  const useServerColumns = hasExplicitSavedView && serverColumns.length > 0;
  const defaults = safeParse(root.dataset.defaultColumns, []);
  const requestedColumns = useServerColumns
    ? serverColumns
    : (localColumns ?? (serverColumns.length ? serverColumns : defaults));
  return {
    columns: normalizeColumns(root, requestedColumns),
    density: useServerColumns
      ? root.dataset.rowDensity || local.density || "standard"
      : local.density || root.dataset.rowDensity || "standard",
    persistAfterInit: useServerColumns
  };
}

function initColumnManager(root) {
  const manager = root.querySelector("[data-column-manager-table]");
  const list = root.querySelector("[data-column-list]");
  let dragging;
  let columnDraft = null;
  let columnDraftConfirmed = false;
  const applyAndPersist = () => {
    const columns = readColumnState(root);
    if (!columns.some((item) => item.visible)) return;
    applyColumns(root, columns);
    persist(root);
  };
  const restoreColumnDraft = () => {
    if (!columnDraft) return;
    applyColumnControls(root, columnDraft);
    columnDraft = null;
  };
  const cancelColumnDraft = (focusSummary = false) => {
    restoreColumnDraft();
    manager?.removeAttribute("open");
    if (focusSummary) manager?.querySelector("summary")?.focus({ preventScroll: true });
  };
  list?.addEventListener("dragstart", (event) => {
    dragging = event.target.closest("[data-column-key]");
    dragging?.classList.add("is-dragging");
  });
  list?.addEventListener("dragover", (event) => {
    event.preventDefault();
    const target = event.target.closest("[data-column-key]");
    if (!dragging || !target || target === dragging) return;
    const bounds = target.getBoundingClientRect();
    list.insertBefore(dragging, event.clientY < bounds.top + bounds.height / 2 ? target : target.nextSibling);
  });
  list?.addEventListener("dragend", () => {
    dragging?.classList.remove("is-dragging");
    dragging = undefined;
  });
  list?.addEventListener("change", (event) => {
    if (!event.target.matches("[data-column-visibility]")) return;
    const visible = Array.from(list.querySelectorAll("[data-column-visibility]")).some((checkbox) => checkbox.checked);
    if (!visible) event.target.checked = true;
  });
  root.querySelector("[data-reset-columns]")?.addEventListener("click", () => {
    const defaults = safeParse(root.dataset.defaultColumns, []);
    applyColumnControls(root, normalizeColumns(root, defaults));
  });
  root.querySelector("[data-show-all-columns]")?.addEventListener("click", () => {
    list?.querySelectorAll("[data-column-visibility]").forEach((checkbox) => { checkbox.checked = true; });
  });
  root.querySelector("[data-confirm-columns]")?.addEventListener("click", () => {
    applyAndPersist();
    columnDraftConfirmed = true;
    columnDraft = null;
    manager?.removeAttribute("open");
    manager?.querySelector("summary")?.focus({ preventScroll: true });
  });
  manager?.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") return;
    cancelColumnDraft(true);
  });
  manager?.addEventListener("toggle", () => {
    if (manager.open) {
      columnDraft = readColumnState(root);
      columnDraftConfirmed = false;
    } else if (columnDraft && !columnDraftConfirmed) {
      restoreColumnDraft();
    }
    columnDraftConfirmed = false;
  });
  document.addEventListener("click", (event) => {
    if (manager?.hasAttribute("open") && !manager.contains(event.target)) cancelColumnDraft();
  });
}

function initDialogs(root) {
  root.querySelectorAll("[data-close-dialog]").forEach((button) => button.addEventListener("click", () => button.closest("dialog")?.close()));
  root.querySelectorAll("dialog").forEach((dialog) => dialog.addEventListener("click", (event) => {
    if (event.target === dialog) dialog.close();
  }));
}

function initRowSpacing(root) {
  root.querySelectorAll("[data-row-spacing]").forEach((button) => button.addEventListener("click", () => {
    applyRowSpacing(root, button.dataset.rowSpacing);
    persist(root);
  }));
}

function initPageSize(root) {
  root.querySelector("[data-current-page-size]")?.addEventListener("change", (event) => {
    if (root.dataset.listPaginationServer !== "true") {
      root.dispatchEvent(new CustomEvent("list-pagination-page-size-change", { detail: { pageSize: event.target.value } }));
      return;
    }
    const url = new URL(window.location.href);
    deleteSearchParamsIgnoreCase(url.searchParams, ["page", "pageNumber", "savedViewId"]);
    url.searchParams.set("pageSize", event.target.value);
    persist(root);
    window.location.assign(url);
  });
}

function initSorting(root) {
  tableFor(root)?.querySelectorAll("[data-sort-key]").forEach((button) => button.addEventListener("click", () => {
    const url = new URL(window.location.href);
    const currentKey = root.dataset.currentSortKey;
    const descending = currentKey === button.dataset.sortKey ? root.dataset.currentSortDescending !== "true" : false;
    deleteSearchParamsIgnoreCase(url.searchParams, ["sortKey", "sortDescending", "sort", "descending", "page", "pageNumber", "savedViewId"]);
    url.searchParams.set("sortKey", button.dataset.sortKey);
    url.searchParams.set("sortDescending", String(descending));
    window.location.assign(url);
  }));
}

export function getWorkbenchTableState(root) {
  return persist(root);
}

export function initDataTables() {
  document.querySelectorAll("[data-workbench]").forEach((root) => {
    const state = initialState(root);
    applyColumns(root, state.columns);
    applyRowSpacing(root, state.density);
    if (state.persistAfterInit) persist(root);
    initColumnManager(root);
    initDialogs(root);
    initRowSpacing(root);
    initPageSize(root);
    initSorting(root);
  });
}
