const themeClasses = ["theme-default", "theme-clear-glass"];
const motionClasses = ["motion-technology", "motion-apple"];
const effectClasses = ["ui-effects-low", "ui-effects-medium", "ui-effects-high"];
const fontClasses = ["font-system-default", "font-microsoft-yahei", "font-microsoft-jhenghei", "font-chinese-serif", "font-chinese-kai"];

function swapClass(classes, selected) {
  document.body.classList.remove(...classes);
  document.body.classList.add(selected);
}

export function initThemePreview() {
  document.querySelectorAll("[data-theme-option] input").forEach((input) => input.addEventListener("change", () => swapClass(themeClasses, input.closest("[data-theme-option]").dataset.themeOption)));
}

function initMotionPreview() {
  document.querySelectorAll("[data-motion-option] input").forEach((input) => input.addEventListener("change", () => swapClass(motionClasses, input.closest("[data-motion-option]").dataset.motionOption)));
  document.querySelectorAll("[data-effects-option] input").forEach((input) => input.addEventListener("change", () => swapClass(effectClasses, input.closest("[data-effects-option]").dataset.effectsOption)));
}

function initFontPreview() {
  const select = document.querySelector("[data-global-font-select]");
  if (!select) return;
  const map = { SystemDefault: "font-system-default", MicrosoftYaHei: "font-microsoft-yahei", MicrosoftJhengHei: "font-microsoft-jhenghei", ChineseSerif: "font-chinese-serif", ChineseKai: "font-chinese-kai" };
  select.addEventListener("change", () => swapClass(fontClasses, map[select.value] || fontClasses[0]));
}

export function initSettingsPreview() {
  initThemePreview();
  initMotionPreview();
  initFontPreview();
}
