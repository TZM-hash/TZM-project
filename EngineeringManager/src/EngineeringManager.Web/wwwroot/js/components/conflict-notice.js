const conflictPattern = /(并发|冲突|已被.+修改|数据已发生变化|请刷新后)/;

export function initConflictNotice() {
  const dialog = document.querySelector("[data-conflict-notice]");
  if (!dialog) return;

  dialog.querySelector("[data-conflict-close]")?.addEventListener("click", () => dialog.close());
  dialog.querySelector("[data-conflict-refresh]")?.addEventListener("click", () => window.location.reload());

  const messages = Array.from(document.querySelectorAll(".validation-summary-errors li, .field-validation-error"))
    .map((element) => element.textContent?.trim())
    .filter((message) => message && conflictPattern.test(message));
  if (messages.length === 0) return;

  dialog.querySelector("[data-conflict-message]").textContent = messages.join("；");
  dialog.showModal();
}
