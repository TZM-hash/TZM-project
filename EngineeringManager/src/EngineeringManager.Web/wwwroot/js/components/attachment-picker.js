export function initAttachmentPickers() {
  document.querySelectorAll("[data-auto-upload-picker]").forEach((form) => {
    const input = form.querySelector("[data-auto-upload-input]");
    const trigger = form.querySelector("[data-auto-upload-trigger]");
    if (!input || !trigger) return;

    trigger.addEventListener("click", () => input.click());
    input.addEventListener("change", () => {
      if (!input.files?.length) return;
      form.requestSubmit();
    });
  });

  document.querySelectorAll("[data-attachment-picker]").forEach((picker) => {
    const input = picker.querySelector('input[type="file"]');
    const name = picker.querySelector("[data-attachment-name]");
    const size = picker.querySelector("[data-attachment-size]");
    const previewLink = picker.querySelector("[data-attachment-preview-link]");
    const image = picker.querySelector("[data-attachment-image]");
    let objectUrl;

    if (!input || !name || !size || !previewLink || !image) return;

    input.addEventListener("change", () => {
      const file = input.files?.[0];
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      objectUrl = undefined;
      name.hidden = !file;
      size.hidden = !file;
      previewLink.hidden = !file;
      image.hidden = !file || !file.type.startsWith("image/");
      if (!file) return;

      objectUrl = URL.createObjectURL(file);
      name.textContent = file.name;
      size.textContent = formatFileSize(file.size);
      previewLink.href = objectUrl;
      if (!image.hidden) image.src = objectUrl;
    });
  });
}

function formatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
