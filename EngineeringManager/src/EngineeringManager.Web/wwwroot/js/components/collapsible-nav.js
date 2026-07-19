const storageKey = "central-ledger-nav-open";

export function initCollapsibleNavigation() {
  document.querySelectorAll("[data-central-ledger-nav]").forEach((group) => {
    const hasActiveChild = Boolean(group.querySelector(".is-active"));
    if (hasActiveChild) group.open = true;
    else {
      try {
        const stored = localStorage.getItem(storageKey);
        if (stored !== null) group.open = stored === "true";
      } catch {
        // Storage can be unavailable in restricted browser contexts.
      }
    }
    group.addEventListener("toggle", () => {
      try { localStorage.setItem(storageKey, String(group.open)); } catch { /* Keep navigation usable without persistence. */ }
    });
  });
}
