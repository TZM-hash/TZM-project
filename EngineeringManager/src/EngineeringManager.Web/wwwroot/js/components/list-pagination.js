import { deleteSearchParamsIgnoreCase } from "./url-search-params.js";

const PAGE_SIZES = [20, 50, 100];
const tableSelector = "table.data-table:not(.sr-only):not([data-list-pagination-disabled])";
const states = new WeakMap();
let observer;
let scanScheduled = false;

function normalizePageSize(value) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  return PAGE_SIZES.includes(parsed) ? parsed : 20;
}

function normalizePage(value, totalPages = 1) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  return Number.isFinite(parsed) ? Math.min(Math.max(parsed, 1), Math.max(totalPages, 1)) : 1;
}

function queryValue(name) {
  const requested = name.toLowerCase();
  for (const [key, value] of new URL(window.location.href).searchParams.entries()) {
    if (key.toLowerCase() === requested) return value;
  }
  return null;
}

function safeStorageRead(key) {
  try {
    const value = JSON.parse(localStorage.getItem(key) || "null");
    return value && typeof value === "object" ? value : {};
  } catch {
    return {};
  }
}

function safeStorageWrite(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* storage unavailable */ }
}

function workbenchForTable(table) {
  const nested = table.closest("[data-workbench]");
  if (nested || !table.id) return nested;
  return Array.from(document.querySelectorAll("[data-workbench][data-table-id]"))
    .find((workbench) => workbench.dataset.tableId === table.id) || null;
}

function tableToken(table, index) {
  if (!table.dataset.listPaginationId) {
    table.dataset.listPaginationId = table.id || table.dataset.listSortId || `table-${index + 1}`;
  }
  return table.dataset.listPaginationId;
}

function storageKey(table, workbench, index) {
  const scope = workbench?.dataset.pageKey || window.location.pathname;
  return `engineering-manager-list-pagination:${scope}:${tableToken(table, index)}`;
}

function headerColumnCount(table) {
  return table.tHead?.rows[table.tHead.rows.length - 1]?.cells.length || 1;
}

function rowIsFixed(row, columnCount) {
  if (row.matches("[data-pagination-fixed], [data-sort-fixed]")) return true;
  const text = String(row.textContent || "").replace(/\s+/g, " ").trim();
  const singleWideCell = row.cells.length === 1 && Number(row.cells[0]?.colSpan || 1) >= Math.max(columnCount, 2);
  const summary = /^(合计|总计|小计|共计|暂无|当前.*暂无|没有符合)/.test(text);
  if (singleWideCell || summary) row.setAttribute("data-pagination-fixed", "");
  return singleWideCell || summary;
}

function rowsFor(table) {
  const columnCount = headerColumnCount(table);
  return Array.from(table.tBodies).flatMap((body) => Array.from(body.rows)).map((row) => ({
    row,
    fixed: rowIsFixed(row, columnCount)
  }));
}

function businessRows(table) {
  return rowsFor(table).filter((item) => !item.fixed).map((item) => item.row);
}

function paginationHost(table, workbench) {
  return table.closest("[data-list-pagination-server]") || workbench || table;
}

function isServerPagination(table, workbench) {
  const host = paginationHost(table, workbench);
  return host.dataset.listPaginationServer === "true"
    || table.dataset.listSortServer === "true"
    || workbench?.dataset.listSortServer === "true";
}

function pageSizePicker(current) {
  const label = document.createElement("label");
  label.className = "page-size-picker";
  const caption = document.createElement("span");
  caption.textContent = "每页";
  const select = document.createElement("select");
  select.dataset.currentPageSize = "";
  select.setAttribute("aria-label", "每页显示条数");
  PAGE_SIZES.forEach((size) => {
    const option = new Option(`${size} 条`, String(size));
    option.selected = size === current;
    select.add(option);
  });
  label.append(caption, select);
  return { label, select };
}

function createNav() {
  const nav = document.createElement("nav");
  nav.className = "table-pagination list-pagination-nav";
  nav.dataset.listPaginationNav = "";
  nav.setAttribute("aria-label", "分页控件");
  return nav;
}

function tableWrapper(table) {
  return table.closest(".table-wrap, .data-table-wrap") || table;
}

function insertAfter(reference, element) {
  reference.parentNode?.insertBefore(element, reference.nextSibling);
}

function readServerMeta(table, select, workbench) {
  const data = paginationHost(table, workbench).dataset;
  const currentPage = normalizePage(data.listPaginationCurrentPage || queryValue("pageNumber") || queryValue("page"));
  const totalPages = Math.max(Number.parseInt(data.listPaginationTotalPages || "1", 10) || 1, 1);
  const totalCount = Math.max(Number.parseInt(data.listPaginationTotalCount || "0", 10) || 0, 0);
  const pageSize = normalizePageSize(data.listPaginationPageSize || select?.value || queryValue("pageSize"));
  return { currentPage: normalizePage(currentPage, totalPages), totalPages, totalCount, pageSize };
}

function readClientState(key, select) {
  const stored = safeStorageRead(key);
  const pageSize = normalizePageSize(stored.pageSize ?? select?.value);
  return { page: Math.max(Number.parseInt(stored.page, 10) || 1, 1), pageSize };
}

function updateSelect(select, pageSize) {
  if (!select) return;
  const valid = PAGE_SIZES.includes(pageSize) ? pageSize : 20;
  if (!Array.from(select.options).some((option) => option.value === String(valid))) {
    PAGE_SIZES.forEach((size) => select.add(new Option(`${size} 条`, String(size))));
  }
  select.value = String(valid);
}

function navigateServer(pageSize, page) {
  const url = new URL(window.location.href);
  deleteSearchParamsIgnoreCase(url.searchParams, ["page", "pageNumber", "savedViewId"]);
  url.searchParams.set("pageSize", String(normalizePageSize(pageSize)));
  if (page > 1) url.searchParams.set("pageNumber", String(page));
  window.location.assign(url);
}

function serverPageUrl(page) {
  const url = new URL(window.location.href);
  deleteSearchParamsIgnoreCase(url.searchParams, ["page", "pageNumber"]);
  if (page > 1) url.searchParams.set("pageNumber", String(page));
  return url.toString();
}

function pageAction(state, label, targetPage, disabled) {
  const control = document.createElement(state.server ? "a" : "button");
  if (!state.server) control.type = "button";
  control.className = "button button--secondary button--small";
  control.textContent = label;
  if (disabled) {
    if (state.server) {
      control.classList.add("is-disabled");
      control.setAttribute("aria-disabled", "true");
    } else {
      control.disabled = true;
    }
  } else if (state.server) {
    control.href = serverPageUrl(targetPage);
  } else {
    control.addEventListener("click", () => {
      state.page = targetPage;
      render(state);
    });
  }
  return control;
}

function pageJump(state, page, totalPages) {
  const form = document.createElement("form");
  form.className = "pagination-page-jump";
  const input = document.createElement("input");
  input.type = "number";
  input.min = "1";
  input.max = String(totalPages);
  input.value = String(page);
  input.setAttribute("aria-label", "跳转页码");
  const submit = document.createElement("button");
  submit.type = "submit";
  submit.className = "button button--secondary button--small";
  submit.textContent = "跳转";
  form.append(input, submit);
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    const targetPage = normalizePage(input.value, totalPages);
    if (state.server) window.location.assign(serverPageUrl(targetPage));
    else {
      state.page = targetPage;
      render(state);
    }
  });
  return form;
}

function renderNavigation(state, totalCount, totalPages, page) {
  if (!state.nav) return;
  state.nav.replaceChildren();
  state.nav.hidden = totalPages <= 1;

  const summary = document.createElement("span");
  summary.textContent = `共 ${totalCount} 条，第 ${page}/${totalPages} 页`;

  const actions = document.createElement("div");
  actions.className = "pagination__actions";
  actions.append(
    pageAction(state, "首页", 1, page <= 1),
    pageAction(state, "上一页", Math.max(1, page - 1), page <= 1),
    pageAction(state, "下一页", Math.min(totalPages, page + 1), page >= totalPages),
    pageAction(state, "末页", totalPages, page >= totalPages),
    pageJump(state, page, totalPages)
  );
  state.nav.append(summary, actions);
}

function persistClientState(state) {
  if (!state.server) safeStorageWrite(state.key, { page: state.page, pageSize: state.pageSize });
}

function render(state) {
  const rows = rowsFor(state.table);
  const business = rows.filter((item) => !item.fixed).map((item) => item.row);
  if (state.server) {
    state.page = normalizePage(state.serverPage, state.totalPages);
    rows.forEach((item) => { item.row.hidden = false; });
    renderNavigation(state, state.totalCount, state.totalPages, state.page);
    return;
  }

  const totalPages = Math.max(1, Math.ceil(business.length / state.pageSize));
  state.page = normalizePage(state.page, totalPages);
  const start = (state.page - 1) * state.pageSize;
  const end = start + state.pageSize;
  let index = 0;
  rows.forEach((item) => {
    item.row.hidden = item.fixed ? false : !(index >= start && index < end);
    if (!item.fixed) index += 1;
  });
  persistClientState(state);
  renderNavigation(state, business.length, totalPages, state.page);
}

function bindPageSize(state) {
  if (!state.select || state.pageSizeBound) return;
  state.pageSizeBound = true;
  if (state.workbench && !state.server) {
    state.workbench.addEventListener("list-pagination-page-size-change", (event) => {
      state.pageSize = normalizePageSize(event.detail?.pageSize);
      state.page = 1;
      updateSelect(state.select, state.pageSize);
      render(state);
    });
    return;
  }
  if (state.server && state.workbench) return;
  state.select.addEventListener("change", (event) => {
    const pageSize = normalizePageSize(event.target.value);
    if (state.server) {
      navigateServer(pageSize, 1);
      return;
    }
    state.pageSize = pageSize;
    state.page = 1;
    render(state);
  });
}

function initTable(table, index) {
  const workbench = workbenchForTable(table);
  const server = isServerPagination(table, workbench);
  const existingSelect = workbench?.querySelector("[data-current-page-size]") || null;
  const key = storageKey(table, workbench, index);
  const serverMeta = server ? readServerMeta(table, existingSelect, workbench) : null;
  const clientMeta = server ? null : readClientState(key, existingSelect);
  const pageSize = serverMeta?.pageSize ?? clientMeta.pageSize;
  const state = states.get(table) || {
    table,
    workbench,
    server,
    key,
    select: existingSelect,
    nav: null,
    pageSize,
    page: serverMeta?.currentPage ?? clientMeta.page,
    serverPage: serverMeta?.currentPage ?? 1,
    totalPages: serverMeta?.totalPages ?? 1,
    totalCount: serverMeta?.totalCount ?? 0
  };
  state.pageSize = pageSize;
  state.server = server;
  state.serverPage = serverMeta?.currentPage ?? state.serverPage;
  state.totalPages = serverMeta?.totalPages ?? state.totalPages;
  state.totalCount = serverMeta?.totalCount ?? state.totalCount;
  states.set(table, state);
  updateSelect(state.select, state.pageSize);

  if (!state.select) {
    const picker = pageSizePicker(state.pageSize);
    state.select = picker.select;
    if (!state.server && !workbench) {
      const bar = document.createElement("div");
      bar.className = "standalone-list-pagination";
      bar.append(picker.label);
      state.nav = createNav();
      bar.append(state.nav);
      insertBeforeTable(table, bar);
    } else if (state.server) {
      const bar = document.createElement("div");
      bar.className = "standalone-list-pagination";
      bar.append(picker.label);
      state.nav = createNav();
      bar.append(state.nav);
      insertBeforeTable(table, bar);
    }
  } else if (!state.server && !state.nav) {
    state.nav = createNav();
    insertAfter(tableWrapper(table), state.nav);
  } else if (state.server && !workbench && !state.nav) {
    state.nav = createNav();
    insertAfter(tableWrapper(table), state.nav);
  }
  bindPageSize(state);
  render(state);
}

function insertBeforeTable(table, element) {
  const wrapper = tableWrapper(table);
  wrapper.parentNode?.insertBefore(element, wrapper);
}

function scan() {
  document.querySelectorAll(tableSelector).forEach((table, index) => initTable(table, index));
}

function scheduleScan() {
  if (scanScheduled) return;
  scanScheduled = true;
  window.requestAnimationFrame(() => {
    scanScheduled = false;
    scan();
  });
}

function mutationNeedsScan(mutation) {
  return Array.from(mutation.addedNodes).some((node) => {
    if (!(node instanceof Element)) return false;
    return node.matches(tableSelector) || node.querySelector?.(tableSelector) || node.matches("tr") || node.querySelector?.("tr");
  }) || Array.from(mutation.removedNodes).some((node) => {
    if (!(node instanceof Element)) return false;
    return node.matches("table, tr") || node.querySelector?.("table, tr");
  });
}

export function initListPagination() {
  scan();
  if (observer || !document.body) return;
  observer = new MutationObserver((mutations) => {
    if (mutations.some(mutationNeedsScan)) scheduleScan();
  });
  observer.observe(document.body, { childList: true, subtree: true });
}
