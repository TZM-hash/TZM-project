import { deleteSearchParamsIgnoreCase } from "./url-search-params.js";

const tableSelector = "table.data-table:not(.sr-only):not([data-list-sort-disabled])";
const collator = new Intl.Collator("zh-CN", { numeric: true, sensitivity: "base" });
const dateHeaderPattern = /日期|时间|月份|年度|创建|更新|发生|签发|开始|结束|进场|退场/;
const businessNumberPattern = /编号|编码/;
const numberHeaderPattern = /金额|数量|比例|进度|单价|人数|总额|应收|应付|已收|已付|未收|未付|开票|余额|差额|税额|费率|百分比/;
const ignoredHeaderPattern = /操作|附件|选择|图片|预览/;
const standalonePickers = new WeakMap();
let observer;
let scanScheduled = false;

function storageKey(table, workbench) {
  const tableId = table.id || table.dataset.listSortId || String(Array.from(document.querySelectorAll(tableSelector)).indexOf(table));
  const scope = workbench?.dataset.pageKey || window.location.pathname;
  return `engineering-manager-list-sort:${scope}:${tableId}`;
}

function parseSelection(value) {
  const separator = String(value || "").lastIndexOf("|");
  if (separator < 0) return { key: "__original", descending: false };
  return {
    key: value.slice(0, separator),
    descending: value.slice(separator + 1) === "true"
  };
}

function selectionValue(key, descending) {
  return `${key}|${String(descending)}`;
}

function normalizedText(value) {
  return String(value || "").replace(/\s+/g, " ").trim();
}

function strictNumberValue(value) {
  const text = normalizedText(value);
  if (!text || /待确认|暂无|未填写|不适用/.test(text)) return null;
  const negativeByParentheses = /^\(.*\)$/.test(text);
  const normalized = text.replace(/[(),，\s￥¥$元%]/g, "");
  if (!/^[+-]?\d+(?:\.\d+)?$/.test(normalized)) return null;
  const parsed = Number(normalized);
  if (!Number.isFinite(parsed)) return null;
  return negativeByParentheses ? -parsed : parsed;
}

function numberValue(value) {
  const strict = strictNumberValue(value);
  if (strict !== null) return strict;
  const text = normalizedText(value);
  if (!text || /待确认|暂无|未填写|不适用/.test(text)) return null;
  const normalized = text.replace(/[，,\s￥¥$元%]/g, "");
  const parenthesized = normalized.match(/\(([+-]?\d+(?:\.\d+)?)\)/);
  const embedded = normalized.match(/[+-]?\d+(?:\.\d+)?/);
  const parsed = Number(parenthesized?.[1] ?? embedded?.[0]);
  if (!Number.isFinite(parsed)) return null;
  return parenthesized ? -Math.abs(parsed) : parsed;
}

function dateValue(value) {
  const text = normalizedText(value);
  if (!text || /待确认|暂无|未填写|长期有效/.test(text)) return null;
  const match = text.match(/(\d{4})[年\-\/.](\d{1,2})(?:[月\-\/.](\d{1,2}))?(?:日)?(?:\s+(\d{1,2}):(\d{2})(?::(\d{2}))?)?/);
  if (!match) return null;
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3] || 1);
  const hour = Number(match[4] || 0);
  const minute = Number(match[5] || 0);
  const second = Number(match[6] || 0);
  const parsed = new Date(year, month - 1, day, hour, minute, second).getTime();
  return Number.isFinite(parsed) ? parsed : null;
}

function headerCells(table) {
  const row = table.tHead?.rows[table.tHead.rows.length - 1];
  if (!row) return [];
  return Array.from(row.cells).map((cell, index) => ({
    cell,
    index,
    key: cell.dataset.sortKey || cell.dataset.columnKey || `column-${index}`,
    serverKey: cell.dataset.sortKey || "",
    label: normalizedText(cell.textContent) || `第 ${index + 1} 列`
  }));
}

function rowIsFixed(row, columnCount) {
  if (row.matches("[data-sort-fixed]")) return true;
  const text = normalizedText(row.textContent);
  const singleWideCell = row.cells.length === 1 && Number(row.cells[0]?.colSpan || 1) >= Math.max(columnCount, 2);
  const summary = /^(合计|总计|小计|共计|暂无|当前.*暂无|没有符合)/.test(text);
  if (singleWideCell || summary) row.setAttribute("data-sort-fixed", "");
  return singleWideCell || summary;
}

function ensureOriginalIndexes(table) {
  Array.from(table.tBodies).forEach((body) => {
    let nextIndex = Array.from(body.rows).reduce((maximum, row) => {
      const current = Number(row.dataset.listSortOriginalIndex);
      return Number.isFinite(current) ? Math.max(maximum, current + 1) : maximum;
    }, 0);
    Array.from(body.rows).forEach((row) => {
      if (row.dataset.listSortOriginalIndex !== undefined) return;
      row.dataset.listSortOriginalIndex = String(nextIndex++);
    });
  });
}

function cellFor(row, header) {
  if (header.key !== `column-${header.index}`) {
    const direct = Array.from(row.children).find((cell) => cell.dataset?.columnKey === header.key);
    if (direct) return direct;
  }
  return row.cells[header.index] || null;
}

function cellValue(row, header) {
  const cell = cellFor(row, header);
  return cell?.dataset.sortValue ?? normalizedText(cell?.textContent);
}

function valueKind(header, rows) {
  if (dateHeaderPattern.test(header.label)) return "date";
  if (numberHeaderPattern.test(header.label)) return "number";
  const samples = rows.map((row) => cellValue(row, header)).filter(Boolean).slice(0, 8);
  if (samples.length > 0 && samples.filter((value) => strictNumberValue(value) !== null).length >= Math.ceil(samples.length * .75)) return "number";
  return "text";
}

function compareNullable(left, right, compare, descending) {
  const leftMissing = left === null || left === undefined || left === "";
  const rightMissing = right === null || right === undefined || right === "";
  if (leftMissing || rightMissing) {
    if (leftMissing && rightMissing) return 0;
    return leftMissing ? 1 : -1;
  }
  const result = compare(left, right);
  return descending ? -result : result;
}

function rowsMatchOrder(current, expected) {
  return current.length === expected.length && current.every((row, index) => row === expected[index]);
}

function sortTable(table, key, descending) {
  ensureOriginalIndexes(table);
  const headers = headerCells(table);
  const header = headers.find((item) => item.key === key);
  let orderChanged = false;
  Array.from(table.tBodies).forEach((body) => {
    const allRows = Array.from(body.rows);
    const fixedRows = allRows.filter((row) => rowIsFixed(row, headers.length));
    const rows = allRows.filter((row) => !fixedRows.includes(row));
    if (rows.length < 2) return;
    const kind = header ? valueKind(header, rows) : "original";
    rows.sort((left, right) => {
      const leftOriginal = Number(left.dataset.listSortOriginalIndex || 0);
      const rightOriginal = Number(right.dataset.listSortOriginalIndex || 0);
      let result;
      if (key === "__original" || !header) {
        result = descending ? rightOriginal - leftOriginal : leftOriginal - rightOriginal;
      } else if (kind === "date") {
        result = compareNullable(dateValue(cellValue(left, header)), dateValue(cellValue(right, header)), (a, b) => a - b, descending);
      } else if (kind === "number") {
        result = compareNullable(numberValue(cellValue(left, header)), numberValue(cellValue(right, header)), (a, b) => a - b, descending);
      } else {
        result = compareNullable(cellValue(left, header), cellValue(right, header), (a, b) => collator.compare(a, b), descending);
      }
      return result || leftOriginal - rightOriginal;
    });
    const orderedRows = [...rows, ...fixedRows];
    if (!rowsMatchOrder(allRows, orderedRows)) {
      orderedRows.forEach((row) => body.appendChild(row));
      orderChanged = true;
    }
  });
  if (orderChanged) observer?.takeRecords();
  table.dataset.listSortKey = key;
  table.dataset.listSortDescending = String(descending);
}

function defaultSort(table, headers) {
  const explicitKey = table.dataset.listSortDefaultKey;
  if (explicitKey) return { key: explicitKey, descending: table.dataset.listSortDefaultDescending !== "false" };
  const dateHeader = headers.find((header) => dateHeaderPattern.test(header.label));
  if (dateHeader) return { key: dateHeader.key, descending: true };
  const businessHeader = headers.find((header) => businessNumberPattern.test(header.label));
  if (businessHeader) return { key: businessHeader.key, descending: true };
  return { key: "__original", descending: false };
}

function optionLabel(header, descending) {
  if (dateHeaderPattern.test(header.label)) return `${header.label}：${descending ? "新到旧" : "旧到新"}`;
  if (numberHeaderPattern.test(header.label)) return `${header.label}：${descending ? "高到低" : "低到高"}`;
  return `${header.label}：${descending ? "降序" : "升序"}`;
}

function addOption(options, seen, key, descending, label) {
  const value = selectionValue(key, descending);
  if (seen.has(value)) return;
  seen.add(value);
  options.push({ value, label });
}

function tableOptions(table) {
  const headers = headerCells(table);
  const defaultDescriptor = defaultSort(table, headers);
  const server = table.dataset.listSortServer === "true";
  const options = [];
  const seen = new Set();
  addOption(options, seen, defaultDescriptor.key, defaultDescriptor.descending, "最新在前");
  addOption(options, seen, defaultDescriptor.key, !defaultDescriptor.descending, "最早在前");
  if (!server && defaultDescriptor.key !== "__original") addOption(options, seen, "__original", false, "原业务顺序");
  headers
    .filter((header) => !ignoredHeaderPattern.test(header.label) && (!server || header.serverKey))
    .forEach((header) => {
      addOption(options, seen, header.key, false, optionLabel(header, false));
      addOption(options, seen, header.key, true, optionLabel(header, true));
    });
  return { options, defaultDescriptor };
}

function createMenu(options) {
  const label = document.createElement("label");
  label.className = "list-sort-picker standalone-list-sort";
  const caption = document.createElement("span");
  caption.textContent = "排序";
  const select = document.createElement("select");
  select.dataset.listSortMenu = "";
  select.setAttribute("aria-label", "选择列表排序方式");
  options.forEach((option) => select.add(new Option(option.label, option.value)));
  label.append(caption, select);
  return { label, select };
}

function readStored(key) {
  try { return localStorage.getItem(key); } catch { return null; }
}

function storeSelection(key, value) {
  try { localStorage.setItem(key, value); } catch { /* local storage unavailable */ }
}

function navigateForServerSort(key, descending) {
  const url = new URL(window.location.href);
  deleteSearchParamsIgnoreCase(url.searchParams, ["sortKey", "sortDescending", "sort", "descending", "page", "pageNumber", "savedViewId"]);
  url.searchParams.set("sortKey", key);
  url.searchParams.set("sortDescending", String(descending));
  window.location.assign(url);
}

function bindMenu(select, table, workbench, server) {
  if (select.dataset.listSortBound === "true") return;
  select.dataset.listSortBound = "true";
  select.addEventListener("change", () => {
    const selection = parseSelection(select.value);
    if (server) {
      navigateForServerSort(selection.key, selection.descending);
      return;
    }
    sortTable(table, selection.key, selection.descending);
    storeSelection(storageKey(table, workbench), select.value);
    if (workbench) {
      workbench.dataset.currentSortKey = selection.key;
      workbench.dataset.currentSortDescending = String(selection.descending);
    }
  });
}

function initWorkbench(workbench) {
  const table = document.getElementById(workbench.dataset.tableId);
  const select = workbench.querySelector("[data-list-sort-menu]");
  if (!select) return;
  const picker = select.closest(".list-sort-picker");
  if (!table) {
    if (picker) picker.hidden = true;
    return;
  }
  const enoughRows = businessRows(table).length >= 2;
  if (picker) picker.hidden = !enoughRows;
  if (!enoughRows) return;
  const server = workbench.dataset.listSortServer === "true";
  if (workbench.dataset.listSortInitialized === "true") {
    ensureOriginalIndexes(table);
    if (!server) {
      const selection = parseSelection(select.value);
      sortTable(table, selection.key, selection.descending);
    }
    return;
  }
  workbench.dataset.listSortInitialized = "true";
  ensureOriginalIndexes(table);
  const currentValue = selectionValue(workbench.dataset.currentSortKey || "__original", workbench.dataset.currentSortDescending === "true");
  const stored = server ? null : readStored(storageKey(table, workbench));
  const requested = stored && Array.from(select.options).some((option) => option.value === stored) ? stored : currentValue;
  if (Array.from(select.options).some((option) => option.value === requested)) select.value = requested;
  bindMenu(select, table, workbench, server);
  if (!server) {
    const selection = parseSelection(select.value);
    sortTable(table, selection.key, selection.descending);
  }
}

function businessRows(table) {
  const columnCount = headerCells(table).length;
  return Array.from(table.tBodies).flatMap((body) => Array.from(body.rows)).filter((row) => !rowIsFixed(row, columnCount));
}

function workbenchForTable(table) {
  const nested = table.closest("[data-workbench]");
  if (nested || !table.id) return nested;
  return Array.from(document.querySelectorAll("[data-workbench][data-table-id]"))
    .find((workbench) => workbench.dataset.tableId === table.id) || null;
}

function initStandalone(table) {
  if (workbenchForTable(table)) return;
  ensureOriginalIndexes(table);
  const enoughRows = businessRows(table).length >= 2;
  const picker = standalonePickers.get(table);
  if (picker) picker.hidden = !enoughRows;
  if (!enoughRows) return;
  if (table.dataset.listSortInitialized === "true") {
    const existing = table.dataset.listSortKey;
    if (existing) sortTable(table, existing, table.dataset.listSortDescending === "true");
    return;
  }

  table.dataset.listSortInitialized = "true";
  const { options, defaultDescriptor } = tableOptions(table);
  const { label, select } = createMenu(options);
  const wrapper = table.closest(".table-wrap, .data-table-wrap") || table;
  wrapper.parentNode?.insertBefore(label, wrapper);
  standalonePickers.set(table, label);
  const server = table.dataset.listSortServer === "true";
  const currentKey = table.dataset.listSortCurrentKey || defaultDescriptor.key;
  const currentDescending = table.dataset.listSortCurrentDescending === "true"
    || (!table.dataset.listSortCurrentDescending && defaultDescriptor.descending);
  const currentValue = selectionValue(currentKey, currentDescending);
  const stored = server ? null : readStored(storageKey(table));
  const requested = stored && options.some((option) => option.value === stored) ? stored : currentValue;
  if (options.some((option) => option.value === requested)) select.value = requested;
  bindMenu(select, table, null, server);
  if (!server) {
    const selection = parseSelection(select.value);
    sortTable(table, selection.key, selection.descending);
  }
}

function scan() {
  document.querySelectorAll("[data-workbench]").forEach(initWorkbench);
  document.querySelectorAll(tableSelector).forEach(initStandalone);
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
  const removedTableRows = Array.from(mutation.removedNodes).some((node) => {
    if (!(node instanceof Element)) return false;
    return node.matches("table, tr") || Boolean(node.querySelector?.("table, tr"));
  });
  if (removedTableRows) return true;
  return Array.from(mutation.addedNodes).some((node) => {
    if (!(node instanceof Element)) return false;
    if (node.matches(tableSelector) || node.querySelector?.(tableSelector)) return true;
    if (node.matches("tr") || node.querySelector?.("tr")) return true;
    return false;
  });
}

export function initListSorting() {
  scan();
  if (observer || !document.body) return;
  observer = new MutationObserver((mutations) => {
    if (mutations.some(mutationNeedsScan)) scheduleScan();
  });
  observer.observe(document.body, { childList: true, subtree: true });
}
