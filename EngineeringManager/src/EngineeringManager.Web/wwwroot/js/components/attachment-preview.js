export function initAttachmentPreview() {
  const dialog = document.querySelector("[data-attachment-preview-dialog]");
  if (!dialog) return;

  const title = dialog.querySelector("[data-attachment-preview-title]");
  const image = dialog.querySelector("[data-attachment-preview-image]");
  const frame = dialog.querySelector("[data-attachment-preview-frame]");
  const fallback = dialog.querySelector("[data-attachment-preview-fallback]");
  const replace = dialog.querySelector("[data-attachment-preview-replace]");
  const remove = dialog.querySelector("[data-attachment-preview-delete]");
  const download = dialog.querySelector("[data-attachment-preview-download]");
  let activeTrigger;

  const setHidden = (element, hidden) => {
    if (element) element.hidden = hidden;
  };

  const downloadUrl = (value) => {
    const url = new URL(value, window.location.href);
    url.searchParams.set("download", "true");
    return url.href;
  };

  const close = () => {
    dialog.close();
    if (image) image.removeAttribute("src");
    if (frame) frame.removeAttribute("src");
    activeTrigger = undefined;
  };

  document.querySelectorAll("[data-attachment-preview-trigger]").forEach((trigger) => {
    trigger.addEventListener("click", (event) => {
      event.preventDefault();
      const source = trigger.dataset.attachmentPreviewUrl || trigger.href;
      if (!source) return;
      activeTrigger = trigger;
      const fileName = trigger.dataset.attachmentName || "附件";
      const contentType = (trigger.dataset.attachmentContentType || "").toLowerCase();
      const isImage = contentType.startsWith("image/");
      const isPdf = contentType === "application/pdf" || fileName.toLowerCase().endsWith(".pdf");
      if (title) title.textContent = fileName;
      setHidden(image, !isImage);
      setHidden(frame, !isPdf);
      setHidden(fallback, isImage || isPdf);
      if (isImage && image) image.src = source;
      if (isPdf && frame) frame.src = source;
      if (fallback) fallback.textContent = isImage || isPdf ? "" : "此文件类型不支持内嵌预览，请下载查看。";
      if (download) {
        download.href = downloadUrl(source);
        download.download = fileName;
      }
      const canManage = trigger.dataset.attachmentCanManage === "true";
      setHidden(replace, !canManage || !trigger.dataset.attachmentReplaceFormId);
      setHidden(remove, !canManage || !trigger.dataset.attachmentDeleteFormId);
      dialog.showModal();
    });
  });

  replace?.addEventListener("click", () => {
    const form = activeTrigger && document.getElementById(activeTrigger.dataset.attachmentReplaceFormId);
    form?.querySelector("input[type='file']")?.click();
  });

  remove?.addEventListener("click", () => {
    const form = activeTrigger && document.getElementById(activeTrigger.dataset.attachmentDeleteFormId);
    form?.requestSubmit();
    close();
  });

  dialog.querySelectorAll("[data-attachment-preview-close]").forEach((button) => button.addEventListener("click", close));
  dialog.addEventListener("cancel", close);
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) close();
  });
}
