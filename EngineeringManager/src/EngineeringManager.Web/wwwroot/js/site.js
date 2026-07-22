import { initShell } from "./core/shell.js";
import { initEffects } from "./core/effects.js";

const jobs = [initShell(), initEffects(), initPwaStatus(), initOfflineDashboard()];
initSmartBack();
initProjectAmountViews();
initProjectGeneralContractors();
initCollectionContractPayerDefaults();
initCompanyAccountEntries();
initProjectContractEditor();
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
if (document.querySelector("[data-central-ledger-nav]")) {
  jobs.push(import("./components/collapsible-nav.js").then((module) => module.initCollapsibleNavigation()));
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

function initProjectAmountViews() {
  document.querySelectorAll("[data-project-amount-view]").forEach((select) => {
    const container = select.closest(".project-amount-switch");
    const update = () => {
      const showSettled = select.value === "settled";
      container.querySelector("[data-project-amount-estimated]").hidden = showSettled;
      container.querySelector("[data-project-amount-settled]").hidden = !showSettled;
    };
    select.addEventListener("change", update);
    update();
  });
}


function initProjectContractEditor() {
  const root = document.querySelector("[data-project-contracts]");
  if (!root) return;

  const list = root.querySelector("[data-project-contract-list]");
  const addButton = root.querySelector("[data-project-contract-add]");
  const totalNode = root.querySelector("[data-project-contract-total]") || document.querySelector("[data-project-contract-total]");
  if (!list) return;

  const initialListHtml = list.innerHTML;

  const reindex = () => {
    const rows = [...list.querySelectorAll("[data-project-contract-row]")];
    rows.forEach((row, index) => {
      row.querySelectorAll("input[name]").forEach((input) => {
        input.name = input.name.replace(/QuickEdit\.Contracts\[\d+\]/, `QuickEdit.Contracts[${index}]`);
      });
    });
    const canRemove = rows.length > 1;
    rows.forEach((row) => {
      row.querySelectorAll("[data-project-contract-remove]").forEach((button) => {
        button.disabled = !canRemove;
        button.hidden = root.closest(".is-editing") ? false : button.hasAttribute("data-inline-edit-control");
      });
    });
    if (addButton) addButton.disabled = rows.length >= 3;
  };

  const refreshTotal = () => {
    if (!totalNode) return;
    let total = 0;
    list.querySelectorAll("[data-project-contract-amount]").forEach((input) => {
      const value = Number.parseFloat(String(input.value || "").replace(/,/g, ""));
      if (Number.isFinite(value)) total += value;
    });
    totalNode.textContent = total.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const bindRow = (row) => {
    row.querySelectorAll("[data-project-contract-amount]").forEach((input) => {
      input.addEventListener("input", refreshTotal);
      input.addEventListener("change", refreshTotal);
    });
    row.querySelectorAll("[data-project-contract-remove]").forEach((button) => {
      button.addEventListener("click", () => {
        const rows = list.querySelectorAll("[data-project-contract-row]");
        if (rows.length <= 1) return;
        row.remove();
        reindex();
        refreshTotal();
      });
    });
  };

  list.querySelectorAll("[data-project-contract-row]").forEach(bindRow);

  addButton?.addEventListener("click", () => {
    const count = list.querySelectorAll("[data-project-contract-row]").length;
    if (count >= 3) return;
    const index = count;
    const row = document.createElement("div");
    row.className = "project-contract-row";
    row.dataset.projectContractRow = "";
    row.dataset.projectContractNew = "true";
    row.innerHTML = `
      <div data-inline-edit-value class="project-contract-display" hidden><strong>新合同</strong><span>未设置</span></div>
      <div class="project-contract-inputs compact-contact-editor inline-cell-control" data-inline-edit-control>
        <input type="hidden" name="QuickEdit.Contracts[${index}].ConcurrencyStamp" value="00000000-0000-0000-0000-000000000000" />
        <input name="QuickEdit.Contracts[${index}].Name" value="" placeholder="合同名称" aria-label="合同名称" data-project-contract-name />
        <input name="QuickEdit.Contracts[${index}].TotalAmount" value="" type="number" step="0.01" min="0" placeholder="合同金额" aria-label="合同金额" data-project-contract-amount />
      </div>
      <button type="button" class="button button--secondary button--action project-contract-row-remove" data-project-contract-remove data-inline-edit-control>删除</button>
    `;
    list.appendChild(row);
    bindRow(row);
    reindex();
    refreshTotal();
    row.querySelector("[data-project-contract-name]")?.focus({ preventScroll: true });
  });

  document.querySelectorAll('[data-inline-edit="project-overview"] [data-inline-edit-cancel]').forEach((button) => {
    button.addEventListener("click", () => {
      list.innerHTML = initialListHtml;
      list.querySelectorAll("[data-project-contract-row]").forEach(bindRow);
      reindex();
      window.setTimeout(refreshTotal, 0);
    });
  });

  // When entering edit mode, ensure remove buttons visible via existing inline-edit toggle.
  document.querySelectorAll('[data-inline-edit="project-overview"] [data-inline-edit-open]').forEach((button) => {
    button.addEventListener("click", () => window.setTimeout(reindex, 0));
  });

  reindex();
  refreshTotal();
}

function initProjectGeneralContractors() {
  const root = document.querySelector("[data-project-contractors]");
  if (!root) return;
  const editor = root.querySelector("[data-project-contractor-editor]");
  const list = root.querySelector("[data-project-contractor-list]");
  const addButton = root.querySelector("[data-project-contractor-add]");
  const cancelButton = root.querySelector("[data-project-contractor-cancel]");
  const confirmButton = root.querySelector("[data-project-contractor-confirm]");
  const countNode = root.querySelector("[data-project-contractor-count]");
  if (!list) return;
  const initialHtml = list.innerHTML;
  let openSnapshotHtml = list.innerHTML;

  const reindex = () => {
    const rows = [...list.querySelectorAll("[data-project-contractor-row]")];
    rows.forEach((row, index) => {
      const input = row.querySelector("[data-project-contractor-name]");
      if (input) {
        input.name = `QuickEdit.GeneralContractorNames[${index}]`;
        input.placeholder = index === 0 ? "主总包单位名称" : "总包单位名称";
      }
      row.querySelectorAll("[data-project-contractor-remove]").forEach((button) => {
        button.disabled = rows.length <= 1;
      });
    });
    if (countNode) {
      const filled = rows.map((row) => row.querySelector("[data-project-contractor-name]")?.value?.trim()).filter(Boolean).length;
      countNode.textContent = filled === 0 ? "未设置" : `${filled}家`;
    }
  };

  const rebind = () => {
    list.querySelectorAll("[data-project-contractor-row]").forEach(bindRow);
    reindex();
  };

  const restoreList = (html) => {
    list.innerHTML = html;
    rebind();
  };

  const closeEditor = () => {
    if (editor) editor.open = false;
  };

  const bindRow = (row) => {
    if (row.dataset.bound === "true") return;
    row.dataset.bound = "true";
    row.querySelectorAll("[data-project-contractor-name]").forEach((input) => {
      input.addEventListener("input", reindex);
    });
    row.querySelectorAll("[data-project-contractor-remove]").forEach((button) => {
      button.addEventListener("click", () => {
        const rows = list.querySelectorAll("[data-project-contractor-row]");
        if (rows.length <= 1) {
          const input = row.querySelector("[data-project-contractor-name]");
          if (input) input.value = "";
          reindex();
          return;
        }
        row.remove();
        reindex();
      });
    });
  };

  rebind();
  addButton?.addEventListener("click", () => {
    const count = list.querySelectorAll("[data-project-contractor-row]").length;
    if (count >= 3) return;
    const row = document.createElement("div");
    row.className = "project-contractor-row";
    row.dataset.projectContractorRow = "";
    row.innerHTML = `
      <input name="QuickEdit.GeneralContractorNames[${count}]" value="" placeholder="总包单位名称" data-project-contractor-name />
      <button type="button" class="button button--ghost button--small" data-project-contractor-remove>删除</button>
    `;
    list.appendChild(row);
    bindRow(row);
    reindex();
    row.querySelector("[data-project-contractor-name]")?.focus({ preventScroll: true });
  });

  editor?.addEventListener("toggle", () => {
    if (editor.open) {
      openSnapshotHtml = list.innerHTML;
      reindex();
    }
  });

  cancelButton?.addEventListener("click", () => {
    restoreList(openSnapshotHtml);
    closeEditor();
  });

  confirmButton?.addEventListener("click", () => {
    reindex();
    closeEditor();
  });

  document.querySelectorAll('[data-inline-edit="project-overview"] [data-inline-edit-cancel]').forEach((button) => {
    button.addEventListener("click", () => {
      restoreList(initialHtml);
      closeEditor();
    });
  });

  reindex();
}

function initCollectionContractPayerDefaults() {
  document.querySelectorAll("[data-collection-entry]").forEach((form) => {
    const contract = form.querySelector("[data-collection-contract]");
    const payer = form.querySelector("[data-collection-payer]");
    if (!contract || !payer) return;

    // 付款方仅来自项目总包时，不再按合同业务伙伴自动改写。
    if (payer.hasAttribute("data-collection-payer-from-contractors")) {
      const options = [...payer.options].filter((option) => option.value);
      if (!payer.value && options.length === 1) {
        payer.value = options[0].value;
      }
      return;
    }

    let defaultPayer = payer.value;
    contract.addEventListener("change", () => {
      const nextPayer = contract.selectedOptions[0]?.dataset.businessPartnerId || "";
      if (!payer.value || payer.value === defaultPayer) payer.value = nextPayer;
      defaultPayer = nextPayer;
    });
  });
}

function initCompanyAccountEntries() {
  document.querySelectorAll("[data-company-account-entry]").forEach((form) => {
    const company = form.querySelector("[data-company-account-company]");
    const account = form.querySelector("[data-company-account-select]");
    if (!company || !account) return;

    const updateAccounts = () => {
      const companyId = company.value.toLowerCase();
      [...account.options].forEach((option) => {
        if (!option.value) return;
        const matches = Boolean(companyId) && option.dataset.legalEntityId?.toLowerCase() === companyId;
        option.hidden = !matches;
        option.disabled = !matches;
      });

      const selected = account.selectedOptions[0];
      if (selected?.value && selected.dataset.legalEntityId?.toLowerCase() !== companyId) account.value = "";
    };

    company.addEventListener("change", updateAccounts);
    updateAccounts();
  });
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
