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

export function initCheckSelectors() {
  document.querySelectorAll("[data-project-export-scope], [data-project-workbook] form").forEach((form) => {
    const allMatching = form.querySelector("[data-project-export-all-matching]");
    const projectItems = Array.from(form.elements).filter((item) => item.matches?.("[data-project-export-item]"));
    const attachmentToggle = form.querySelector("[data-project-export-attachments]");
    const attachmentSheet = form.querySelector('[data-project-workbook-sheet="Attachments"]');
    projectItems.forEach((item) => item.addEventListener("change", () => {
      if (item.checked && allMatching) allMatching.checked = false;
      updateProjectSelectionCount(form);
    }));
    allMatching?.addEventListener("change", () => {
      if (allMatching.checked) projectItems.forEach((item) => { item.checked = false; });
      updateProjectSelectionCount(form);
    });
    attachmentToggle?.addEventListener("change", () => {
      if (attachmentSheet) attachmentSheet.checked = attachmentToggle.checked;
    });
    attachmentSheet?.addEventListener("change", () => {
      if (attachmentToggle) attachmentToggle.checked = attachmentSheet.checked;
    });
    updateProjectSelectionCount(form);
  });

  document.querySelectorAll("[data-check-selector]").forEach((root) => {
    const isProjectExportMenu = root.matches("[data-project-workbook-export-menu]");
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
    if (options().length > 0) {
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
        checkSelectorSnapshot = captureCheckSelectorSnapshot();
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
