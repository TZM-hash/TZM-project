function updateCount(root) {
  const count = root.querySelectorAll("[data-check-selector-option]:checked").length;
  const target = root.querySelector("[data-check-selector-count]");
  if (target) target.textContent = count > 0 ? `已选 ${count} 项` : root.dataset.checkSelectorEmptyLabel || "未选择";
}

function ensureCheckSelectorConfirm(root) {
  let button = root.querySelector("[data-check-selector-confirm]");
  if (button) return button;
  const menu = root.querySelector(".selection-dropdown-menu");
  if (!menu) return null;
  const actions = document.createElement("div");
  actions.className = "selection-dropdown-actions";
  button = document.createElement("button");
  button.type = "button";
  button.className = "button button--primary button--small";
  button.dataset.checkSelectorConfirm = "";
  button.textContent = "确认";
  actions.appendChild(button);
  menu.appendChild(actions);
  return button;
}

function updateProjectSelectionCount(form) {
  const target = form.querySelector("[data-project-export-selected-count]");
  const selected = Array.from(form.elements).filter((item) => item.matches?.("[data-project-export-item]:checked")).length;
  if (target) target.textContent = selected;
}

function initProjectExportFilters(form) {
  const filters = Array.from(form.elements).filter((item) => item.matches?.("[data-project-export-filter]"));
  const target = form.querySelector("[data-project-export-filter-count]");
  const clear = form.querySelector("[data-project-export-filter-clear]");
  const update = () => {
    const count = filters.filter((item) => String(item.value || "").trim() !== "").length;
    if (target) target.textContent = `${count} 项`;
  };
  filters.forEach((item) => item.addEventListener("change", update));
  filters.forEach((item) => item.addEventListener("input", update));
  clear?.addEventListener("click", () => {
    filters.forEach((item) => { item.value = ""; });
    update();
  });
  update();
}

function revealProjectExportError(error, details) {
  if (!error) return;
  if (details) details.open = true;
  error.hidden = false;
  error.setAttribute("tabindex", "-1");
  requestAnimationFrame(() => {
    error.scrollIntoView({ block: "nearest", behavior: "smooth" });
    error.focus({ preventScroll: true });
  });
}

function projectExportResponseFileName(response) {
  const disposition = response.headers.get("Content-Disposition") || "";
  const encoded = disposition.match(/filename\*\s*=\s*UTF-8''([^;]+)/i)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded.trim().replace(/^"|"$/g, ""));
    } catch {
      // Fall through to the legacy filename parameter when decoding fails.
    }
  }
  const plain = disposition.match(/filename\s*=\s*"?([^";]+)"?/i)?.[1];
  if (plain) return plain.trim();
  return (response.headers.get("Content-Type") || "").toLowerCase().includes("zip")
    ? "项目工作簿.zip"
    : "项目清单.xlsx";
}

async function downloadProjectExportResponse(response) {
  const contentType = (response.headers.get("Content-Type") || "").toLowerCase();
  if (contentType.includes("text/html")) {
    const html = await response.text();
    document.open();
    document.write(html);
    document.close();
    return;
  }
  if (!response.ok) throw new Error(`导出请求失败（${response.status}）`);

  const objectUrl = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = objectUrl;
  link.download = projectExportResponseFileName(response);
  link.hidden = true;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
}

function initProjectExportSubmitFeedback(form) {
  const button = form.querySelector("[data-project-export-submit]");
  if (!button) return;
  let submitting = false;
  const idleLabel = button.dataset.projectExportIdleLabel || "生成项目工作簿";
  const reset = () => {
    submitting = false;
    form.removeAttribute("aria-busy");
    button.disabled = false;
    button.classList.remove("is-loading");
    button.textContent = idleLabel;
  };
  const showFailure = () => {
    const error = form.querySelector("[data-project-export-scope-error]");
    if (!error) return;
    error.textContent = "导出失败，请检查网络连接后重试。";
    error.hidden = false;
  };
  form.addEventListener("submit", (event) => {
    if (event.defaultPrevented) return;
    if (submitting) {
      event.preventDefault();
      return;
    }
    submitting = true;
    form.setAttribute("aria-busy", "true");
    button.disabled = true;
    button.classList.add("is-loading");
    button.textContent = "正在生成…";
    event.preventDefault();
    fetch(form.action || window.location.href, {
      method: form.method || "post",
      body: new FormData(form),
      credentials: "same-origin",
      headers: { Accept: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, application/zip, text/html" }
    })
      .then(downloadProjectExportResponse)
      .catch((error) => {
        console.error("项目工作簿导出失败", error);
        showFailure();
      })
      .finally(reset);
  });
}

export function initCheckSelectors() {
  document.querySelectorAll("[data-project-export-scope], [data-project-workbook] form").forEach((form) => {
    const allMatching = form.querySelector("[data-project-export-all-matching]");
    const projectItems = Array.from(form.elements).filter((item) => item.matches?.("[data-project-export-item]"));
    const exportColumns = Array.from(form.elements).filter((item) => item.matches?.("[data-project-export-columns]"));
    const tableExportColumns = Array.from(form.elements).filter((item) => item.matches?.("[name='TableExportColumns']"));
    const columnModes = Array.from(form.elements).filter((item) => item.matches?.("[name='ExportColumnMode']"));
    const scopeError = form.querySelector("[data-project-export-scope-error]");
    const attachmentToggle = form.querySelector("[data-project-export-attachments]");
    const attachmentSheet = form.querySelector('[data-project-workbook-sheet="Attachments"]');
    const hasProjectScope = () => Boolean(allMatching?.checked || projectItems.some((item) => item.checked));
    const updateProjectScopeError = () => {
      if (scopeError) scopeError.hidden = hasProjectScope();
    };
    projectItems.forEach((item) => item.addEventListener("change", () => {
      if (item.checked && allMatching) allMatching.checked = false;
      updateProjectSelectionCount(form);
      updateProjectScopeError();
    }));
    allMatching?.addEventListener("change", () => {
      if (allMatching.checked) projectItems.forEach((item) => { item.checked = false; });
      updateProjectSelectionCount(form);
      updateProjectScopeError();
    });
    attachmentToggle?.addEventListener("change", () => {
      if (attachmentToggle.checked && attachmentSheet) attachmentSheet.checked = true;
    });
    if (exportColumns.length > 0) {
      const error = form.querySelector("[data-project-export-columns-error]");
      const columnDetails = form.querySelector("[data-project-export-columns-details]");
      const selectedColumnMode = () => form.querySelector("[name='ExportColumnMode']:checked")?.value || "content";
      const hasExportColumns = () => selectedColumnMode() === "table"
        ? tableExportColumns.some((item) => !item.disabled && String(item.value || "").trim() !== "")
        : exportColumns.some((item) => item.checked);
      const updateExportColumnError = () => {
        if (error) error.hidden = hasExportColumns();
      };
      exportColumns.forEach((item) => item.addEventListener("change", updateExportColumnError));
      tableExportColumns.forEach((item) => item.addEventListener("change", updateExportColumnError));
      columnModes.forEach((item) => item.addEventListener("change", updateExportColumnError));
      form.addEventListener("submit", (event) => {
        if (hasExportColumns()) return;
        event.preventDefault();
        revealProjectExportError(error, columnDetails);
      });
      updateExportColumnError();
    }
    if (scopeError) {
      form.addEventListener("submit", (event) => {
        if (hasProjectScope()) {
          scopeError.hidden = true;
          return;
        }
        event.preventDefault();
        revealProjectExportError(scopeError);
      });
    }
    initProjectExportFilters(form);
    updateProjectSelectionCount(form);
    initProjectExportSubmitFeedback(form);
  });

  document.querySelectorAll("[data-check-selector]").forEach((root) => {
    const isProjectExportMenu = root.matches("[data-project-workbook-export-menu]");
    const persistsOnClose = root.hasAttribute("data-check-selector-persist");
    const syncProjectExportPosition = () => {
      if (!isProjectExportMenu || !root.open || window.matchMedia("(max-width: 720px)").matches) {
        root.classList.remove("project-export-opens-down");
        root.style.removeProperty("--project-export-max-height");
        return;
      }

      const bounds = root.getBoundingClientRect();
      const headerHeight = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--header-height")) || 0;
      const gutter = 8;
      const spaceAbove = Math.max(0, bounds.top - headerHeight - gutter);
      const spaceBelow = Math.max(0, window.innerHeight - bounds.bottom - gutter);
      root.classList.toggle("project-export-opens-down", spaceBelow >= spaceAbove);
      root.style.setProperty("--project-export-max-height", `${Math.max(spaceAbove, spaceBelow)}px`);
    };
    const syncProjectExportOpenState = () => {
      if (isProjectExportMenu) {
        document.body.classList.toggle("project-export-open", root.open);
        if (root.open) requestAnimationFrame(syncProjectExportPosition);
        else syncProjectExportPosition();
      }
    };
    const options = () => Array.from(root.querySelectorAll("[data-check-selector-option]"));
    let checkSelectorSnapshot = null;
    let checkSelectorConfirmed = false;
    const captureCheckSelectorSnapshot = () => options().map((option) => option.checked);
    const restoreCheckSelectorSnapshot = () => {
      if (!checkSelectorSnapshot) return;
      const changed = [];
      options().forEach((option, index) => {
        if (option.checked !== checkSelectorSnapshot[index]) changed.push(option);
        option.checked = checkSelectorSnapshot[index];
      });
      updateCount(root);
      changed.forEach((option) => option.dispatchEvent(new Event("change", { bubbles: true })));
      checkSelectorSnapshot = null;
    };
    const cancelCheckSelector = (focusSummary = false) => {
      restoreCheckSelectorSnapshot();
      root.removeAttribute("open");
      if (focusSummary) root.querySelector("summary")?.focus({ preventScroll: true });
    };
    root.addEventListener("change", (event) => {
      if (event.target.matches("[data-check-selector-option]")) updateCount(root);
    });
    root.querySelector("[data-check-selector-all]")?.addEventListener("click", () => {
      options().forEach((option) => { option.checked = true; });
      updateCount(root);
    });
    root.querySelector("[data-check-selector-default]")?.addEventListener("click", () => {
      options().forEach((option) => { option.checked = option.dataset.default === "true"; });
      updateCount(root);
    });
    root.querySelector("[data-check-selector-clear]")?.addEventListener("click", () => {
      options().forEach((option) => { option.checked = false; });
      updateCount(root);
    });
    if (options().length > 0 && !persistsOnClose) {
      ensureCheckSelectorConfirm(root)?.addEventListener("click", () => {
        checkSelectorConfirmed = true;
        checkSelectorSnapshot = null;
        root.removeAttribute("open");
        root.dispatchEvent(new CustomEvent("check-selector:confirmed", { bubbles: true }));
        root.querySelector("summary")?.focus({ preventScroll: true });
      });
    }
    root.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      cancelCheckSelector(true);
    });
    root.addEventListener("toggle", () => {
      if (root.open) {
        checkSelectorSnapshot = persistsOnClose ? null : captureCheckSelectorSnapshot();
        checkSelectorConfirmed = false;
      } else if (checkSelectorSnapshot && !checkSelectorConfirmed) {
        restoreCheckSelectorSnapshot();
      }
      checkSelectorConfirmed = false;
      syncProjectExportOpenState();
    });
    if (isProjectExportMenu) {
      window.addEventListener("resize", syncProjectExportPosition);
      window.addEventListener("scroll", syncProjectExportPosition, { passive: true });
    }
    syncProjectExportOpenState();
    updateCount(root);
  });

  document.addEventListener("click", (event) => {
    document.querySelectorAll("[data-check-selector][open]").forEach((root) => {
      if (!root.contains(event.target)) {
        root.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      }
    });
  });
}
