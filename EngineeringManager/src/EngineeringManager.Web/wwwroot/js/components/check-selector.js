function updateCount(root) {
  const count = root.querySelectorAll("[data-check-selector-option]:checked").length;
  const target = root.querySelector("[data-check-selector-count]");
  if (target) target.textContent = count > 0 ? `已选 ${count} 项` : root.dataset.checkSelectorEmptyLabel || "未选择";
}

function updateProjectSelectionCount(form) {
  const target = form.querySelector("[data-project-export-selected-count]");
  if (target) target.textContent = form.querySelectorAll("[data-project-export-item]:checked").length;
}

export function initCheckSelectors() {
  document.querySelectorAll("[data-project-export-scope], [data-project-workbook] form").forEach((form) => {
    const allMatching = form.querySelector("[data-project-export-all-matching]");
    const projectItems = Array.from(form.querySelectorAll("[data-project-export-item]"));
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
    const options = () => Array.from(root.querySelectorAll("[data-check-selector-option]"));
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
    root.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      root.removeAttribute("open");
      root.querySelector("summary")?.focus({ preventScroll: true });
    });
    updateCount(root);
  });

  document.addEventListener("click", (event) => {
    document.querySelectorAll("[data-check-selector][open]").forEach((root) => {
      if (!root.contains(event.target)) root.removeAttribute("open");
    });
  });
}
