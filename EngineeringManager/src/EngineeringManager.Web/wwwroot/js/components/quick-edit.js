function setEditorState(editor, editing) {
  editor.classList.toggle("is-editing", editing);
  if (editor.hasAttribute("data-inline-cell-edit")) {
    editor.querySelectorAll("[data-inline-edit-value]").forEach((element) => { element.hidden = editing; });
    editor.querySelectorAll("[data-inline-edit-control]").forEach((element) => { element.hidden = !editing; });
    editor.querySelectorAll("[data-inline-edit-actions]").forEach((element) => { element.hidden = !editing; });
  } else if (editor.hasAttribute("data-inline-entry-edit")) {
    editor.querySelectorAll("[data-inline-edit-view]").forEach((element) => { element.hidden = false; });
    editor.querySelectorAll("[data-inline-edit-form]").forEach((element) => { element.hidden = !editing; });
  } else {
    editor.querySelectorAll("[data-inline-edit-view]").forEach((element) => { element.hidden = editing; });
    editor.querySelectorAll("[data-inline-edit-form]").forEach((element) => { element.hidden = !editing; });
  }
  editor.querySelectorAll("[data-inline-edit-open]").forEach((button) => button.setAttribute("aria-expanded", String(editing)));
  if (editing) editor.querySelector("[data-inline-edit-control]:not([type='hidden']), [data-inline-edit-form] input:not([type='hidden']), [data-inline-edit-form] select, [data-inline-edit-form] textarea")?.focus({ preventScroll: true });
}

function populateFields(container, values) {
  Object.entries(values).forEach(([key, value]) => {
    const field = container.querySelector(`[data-inline-edit-field="${key}"]`);
    if (!field) return;
    if (field.type === "checkbox") field.checked = Boolean(value);
    else field.value = value === null || value === undefined ? "" : String(value).toLowerCase() === "true" || String(value).toLowerCase() === "false" ? String(value).toLowerCase() : String(value);
  });
  const detailLink = container.querySelector("[data-inline-edit-detail-link]");
  if (detailLink && values.Id) detailLink.href = detailLink.dataset.detailHrefTemplate.replace("{Id}", values.Id);
}

function updateKindFields(select) {
  const editor = select.closest("[data-inline-edit]");
  if (!editor) return;
  editor.querySelectorAll("[data-kind]").forEach((element) => {
    const kinds = element.dataset.kind.split(",").map((value) => value.trim());
    element.hidden = !kinds.includes(select.value);
    element.querySelectorAll("input, select, textarea").forEach((field) => { field.disabled = element.hidden; });
  });
}

function updateConstructionEquipmentFields(select) {
  const container = select.closest("form, [data-construction-row]");
  if (!container) return;
  const isEquipment = select.value === "1";
  container.querySelectorAll("[data-construction-equipment-only]").forEach((element) => {
    element.hidden = !isEquipment;
    element.querySelectorAll("input").forEach((field) => {
      field.disabled = !isEquipment;
      if (!isEquipment && field.type === "checkbox") field.checked = false;
    });
  });
}

function updateProjectScopedOptions(projectSelect) {
  const form = projectSelect.closest("form");
  if (!form) return;
  form.querySelectorAll("[data-project-scoped-select]").forEach((select) => {
    let selectedIsVisible = !select.value;
    select.querySelectorAll("option[data-project-id]").forEach((option) => {
      const visible = option.dataset.projectId === projectSelect.value;
      option.hidden = !visible;
      option.disabled = !visible;
      if (visible && option.value === select.value) selectedIsVisible = true;
    });
    if (!selectedIsVisible) select.value = "";
  });
}

export function initInlineEditors() {
  document.querySelectorAll("[data-inline-edit]").forEach((editor) => {
    editor.querySelectorAll("[data-inline-edit-open]").forEach((button) => button.addEventListener("click", () => setEditorState(editor, true)));
    editor.querySelectorAll("[data-inline-edit-cancel]").forEach((button) => button.addEventListener("click", () => {
      editor.querySelectorAll("[data-check-selector][open]").forEach((selector) => {
        selector.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      });
      editor.querySelectorAll("form").forEach((form) => form.reset());
      editor.querySelectorAll("[data-check-selector]").forEach((selector) => {
        selector.querySelector("[data-check-selector-option]")?.dispatchEvent(new Event("change", { bubbles: true }));
      });
      setEditorState(editor, false);
      editor.querySelectorAll("[data-inline-edit-kind-select]").forEach(updateKindFields);
      editor.querySelectorAll("[data-project-amount-view]").forEach((select) => select.dispatchEvent(new Event("change")));
    }));
    setEditorState(editor, editor.dataset.inlineEditActive === "true");
  });

  document.querySelectorAll("[data-inline-edit-table]").forEach((tableEditor) => {
    const editorRow = tableEditor.querySelector("[data-inline-edit-table-row]");
    if (!editorRow) return;
    tableEditor.querySelectorAll("[data-inline-edit-table-open]").forEach((button) => button.addEventListener("click", () => {
      populateFields(editorRow, JSON.parse(button.dataset.inlineEditValues || "{}"));
      button.closest("tr")?.after(editorRow);
      editorRow.hidden = false;
      editorRow.classList.add("is-editing");
      button.setAttribute("aria-expanded", "true");
      editorRow.querySelector("input:not([type='hidden']), select, textarea")?.focus({ preventScroll: true });
    }));
    editorRow.querySelectorAll("[data-inline-edit-cancel]").forEach((button) => button.addEventListener("click", () => {
      editorRow.querySelector("form")?.reset();
      editorRow.hidden = true;
      editorRow.classList.remove("is-editing");
      tableEditor.querySelectorAll("[data-inline-edit-table-open]").forEach((openButton) => openButton.setAttribute("aria-expanded", "false"));
    }));
    if (editorRow.dataset.inlineEditActive === "true") editorRow.hidden = false;
  });

  document.querySelectorAll("[data-inline-edit-kind-select]").forEach((select) => {
    select.addEventListener("change", () => updateKindFields(select));
    updateKindFields(select);
  });

  const recalculateConstruction = (row) => {
    const entry = row.querySelector("[data-construction-entry]")?.value;
    if (!entry) return;
    const exit = row.querySelector("[data-construction-exit]")?.value;
    const end = exit ? new Date(`${exit}T00:00:00`) : new Date();
    const start = new Date(`${entry}T00:00:00`);
    const total = Math.max(0, Math.floor((end - start) / 86400000) + 1);
    const stop = Number(row.querySelector("[data-construction-stop]")?.value || 0);
    const totalNode = row.querySelector("[data-construction-total]");
    const workNode = row.querySelector("[data-construction-work]");
    if (totalNode) totalNode.textContent = String(total);
    if (workNode) workNode.textContent = String(Math.max(0, total - stop));
  };
  document.querySelectorAll("[data-construction-row]").forEach((row) => {
    row.querySelectorAll("[data-construction-entry], [data-construction-exit], [data-construction-stop]").forEach((field) => field.addEventListener("input", () => recalculateConstruction(row)));
  });

  document.querySelectorAll("[data-construction-type]").forEach((select) => {
    select.addEventListener("change", () => updateConstructionEquipmentFields(select));
    updateConstructionEquipmentFields(select);
  });

  document.querySelectorAll("[data-finance-project-select]").forEach((select) => {
    select.addEventListener("change", () => updateProjectScopedOptions(select));
    updateProjectScopedOptions(select);
  });

  document.querySelector(".is-target-record")?.scrollIntoView({ behavior: "smooth", block: "center" });
}
