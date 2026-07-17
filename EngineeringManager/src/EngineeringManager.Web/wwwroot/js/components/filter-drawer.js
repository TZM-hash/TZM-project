function removeFilter(key) {
  const url = new URL(window.location.href);
  url.searchParams.delete(key);
  url.searchParams.delete("page");
  window.location.assign(url);
}

function clearFilters(root) {
  const url = new URL(window.location.href);
  root.querySelectorAll("[data-filter-key]").forEach((control) => url.searchParams.delete(control.dataset.filterKey));
  url.searchParams.delete("page");
  window.location.assign(url);
}

export function initFilterDrawers() {
  document.querySelectorAll("[data-workbench]").forEach((root) => {
    const drawer = root.querySelector("[data-filter-drawer]");
    root.querySelector("[data-open-filter-drawer]")?.addEventListener("click", () => drawer?.showModal());
    root.querySelectorAll("[data-filter-chip]").forEach((chip) => chip.addEventListener("click", () => removeFilter(chip.dataset.filterKey)));
    root.querySelector("[data-clear-filter-chips]")?.addEventListener("click", () => clearFilters(root));
    root.querySelector("[data-reset-filter-form]")?.addEventListener("click", () => {
      root.querySelector("[data-filter-form]")?.reset();
      root.querySelectorAll("[data-filter-form] [data-filter-key]").forEach((control) => { control.value = ""; });
    });
    root.querySelector("[data-filter-form]")?.addEventListener("submit", () => {
      const page = root.querySelector("[data-filter-form] input[name='page']");
      if (page) page.remove();
    });
  });
}
