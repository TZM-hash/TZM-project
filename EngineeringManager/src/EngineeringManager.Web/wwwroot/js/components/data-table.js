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

function applyRowSpacing(root, spacing) {
  const value = ["compact", "standard", "spacious"].includes(spacing) ? spacing : "standard";
  root.classList.remove(...rowSpacingClasses);
  root.classList.add(`row-spacing-${value}`);
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

function initialState(root) {
  const serverColumns = safeParse(root.dataset.savedViewColumns, []);
  if (serverColumns.length) return { columns: normalizeColumns(root, serverColumns), density: root.dataset.rowDensity || "standard" };
  let local = {};
  try { local = safeParse(localStorage.getItem(storageKey(root)), {}); } catch { local = {}; }
  const defaults = safeParse(root.dataset.defaultColumns, []);
  return { columns: normalizeColumns(root, local.columns?.length ? local.columns : defaults), density: local.density || root.dataset.rowDensity || "standard" };
}

function initColumnManager(root) {
  const dialog = root.querySelector("[data-column-manager-table]");
  const list = root.querySelector("[data-column-list]");
  let dragging;
  root.querySelector("[data-open-column-manager]")?.addEventListener("click", () => dialog?.showModal());
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
  list?.addEventListener("dragend", () => { dragging?.classList.remove("is-dragging"); dragging = undefined; });
  list?.addEventListener("change", (event) => {
    if (!event.target.matches("[data-column-visibility]") || event.target.checked) return;
    const visible = Array.from(list.querySelectorAll("[data-column-visibility]")).some((checkbox) => checkbox.checked);
    if (!visible) event.target.checked = true;
  });
  root.querySelector("[data-apply-columns]")?.addEventListener("click", () => {
    const columns = readColumnState(root);
    if (!columns.some((item) => item.visible)) return;
    applyColumns(root, columns);
    persist(root);
    dialog?.close();
  });
  root.querySelector("[data-reset-columns]")?.addEventListener("click", () => {
    const defaults = safeParse(root.dataset.defaultColumns, []);
    applyColumns(root, normalizeColumns(root, defaults));
  });
}

function initDialogs(root) {
  root.querySelectorAll("[data-close-dialog]").forEach((button) => button.addEventListener("click", () => button.closest("dialog")?.close()));
}

function initRowSpacing(root) {
  root.querySelectorAll("[data-row-spacing]").forEach((button) => button.addEventListener("click", () => {
    applyRowSpacing(root, button.dataset.rowSpacing);
    persist(root);
  }));
}

function initPageSize(root) {
  root.querySelector("[data-current-page-size]")?.addEventListener("change", (event) => {
    const url = new URL(window.location.href);
    url.searchParams.set("pageSize", event.target.value);
    url.searchParams.delete("page");
    persist(root);
    window.location.assign(url);
  });
}

export function getWorkbenchTableState(root) {
  return persist(root);
}

export function initDataTables() {
  document.querySelectorAll("[data-workbench]").forEach((root) => {
    const state = initialState(root);
    applyColumns(root, state.columns);
    applyRowSpacing(root, state.density);
    initColumnManager(root);
    initDialogs(root);
    initRowSpacing(root);
    initPageSize(root);
  });
}
