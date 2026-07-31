const themeClasses = ["theme-default", "theme-clear-glass"];
const motionClasses = ["motion-technology", "motion-apple"];
const effectClasses = ["ui-effects-low", "ui-effects-medium", "ui-effects-high"];
const fontClasses = ["font-system-default", "font-microsoft-yahei", "font-microsoft-jhenghei", "font-chinese-serif", "font-chinese-kai"];
const fontSizeClasses = ["font-size-small", "font-size-standard", "font-size-large", "font-size-extra-large"];

function swapClass(classes, selected) {
  document.body.classList.remove(...classes);
  document.body.classList.add(selected);
}

function swapRootClass(classes, selected) {
  document.documentElement.classList.remove(...classes);
  document.documentElement.classList.add(selected);
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

function initFontSizePreview() {
  const select = document.querySelector("[data-global-font-size-select]");
  if (!select) return;
  const map = { Small: "font-size-small", Standard: "font-size-standard", Large: "font-size-large", ExtraLarge: "font-size-extra-large" };
  select.addEventListener("change", () => swapRootClass(fontSizeClasses, map[select.value] || "font-size-standard"));
}

export function initSettingsPreview() {
  initThemePreview();
  initMotionPreview();
  initFontPreview();
  initFontSizePreview();
}
