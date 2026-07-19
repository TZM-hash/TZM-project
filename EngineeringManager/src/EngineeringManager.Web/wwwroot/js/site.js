import { initShell } from "./core/shell.js";
import { initEffects } from "./core/effects.js";

const jobs = [initShell(), initEffects(), initPwaStatus(), initOfflineDashboard()];
initSmartBack();
if (document.querySelector("[data-conflict-notice]")) {
  jobs.push(import("./components/conflict-notice.js").then((module) => module.initConflictNotice()));
}
if (document.querySelector("[data-theme-option], [data-motion-option], [data-global-font-picker]")) {
  jobs.push(import("./pages/settings.js").then((module) => module.initSettingsPreview()));
}
if (document.querySelector("[data-workbench]")) {
  jobs.push(Promise.all([
    import("./components/data-table.js"),
    import("./components/saved-views.js"),
    import("./components/filter-drawer.js")
  ]).then(([tables, views, filters]) => {
    tables.initDataTables();
    views.initSavedViews();
    filters.initFilterDrawers();
  }));
}
if (document.querySelector("[data-chart]")) {
  jobs.push(import("./components/charts.js").then((module) => module.initCharts()));
}
if (document.querySelector("[data-inline-edit], [data-inline-edit-table], [data-finance-project-select]")) {
  jobs.push(import("./components/quick-edit.js").then((module) => module.initInlineEditors()));
}
if (document.querySelector("[data-check-selector]")) {
  jobs.push(import("./components/check-selector.js").then((module) => module.initCheckSelectors()));
}
await Promise.all(jobs);

function initSmartBack() {
  document.querySelectorAll("[data-smart-back]").forEach((link) => link.addEventListener("click", (event) => {
    if (!document.referrer) return;
    const referrer = new URL(document.referrer);
    if (referrer.origin !== window.location.origin || window.history.length <= 1) return;
    event.preventDefault();
    window.history.back();
  }));
}

async function initPwaStatus() {
  const badge = document.querySelector("[data-pwa-badge]");
  const status = document.querySelector("[data-pwa-status]");
  const showStatus = (message) => {
    if (badge) badge.textContent = message;
    if (status) status.textContent = message;
  };
  if (!("serviceWorker" in navigator) || !(window.isSecureContext || window.location.hostname === "localhost")) {
    showStatus("在线模式");
    return;
  }
  try {
    const registration = await navigator.serviceWorker.register("/service-worker.js");
    showStatus(registration.waiting ? "发现新版本，请刷新页面" : "离线外壳可用");
    registration.addEventListener("updatefound", () => {
      registration.installing?.addEventListener("statechange", (event) => {
        if (event.target.state === "installed" && navigator.serviceWorker.controller) showStatus("发现新版本，请刷新页面");
      });
    });
  } catch {
    showStatus("在线模式");
  }
}

async function initOfflineDashboard() {
  const dashboard = document.querySelector("[data-dashboard-offline]");
  if (!dashboard) return;
  let counts = { pending: 0, failed: 0, conflicts: 0 };
  try {
    counts = JSON.parse(localStorage.getItem(`engineering-manager-offline-counts:${dashboard.dataset.userId}`) || "{}") || counts;
  } catch {
    counts = { pending: 0, failed: 0, conflicts: 0 };
  }
  dashboard.querySelector("[data-dashboard-pending]").textContent = counts.pending || 0;
  dashboard.querySelector("[data-dashboard-failed]").textContent = counts.failed || 0;
  dashboard.querySelector("[data-dashboard-conflicts]").textContent = counts.conflicts || 0;
}
