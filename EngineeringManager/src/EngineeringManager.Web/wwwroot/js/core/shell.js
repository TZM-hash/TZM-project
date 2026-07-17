export function initSidebar() {
  const toggle = document.querySelector("[data-menu-toggle]");
  const sidebar = document.querySelector("[data-navigation]");
  if (!toggle || !sidebar) return;
  const mobile = () => window.matchMedia("(max-width: 760px)").matches;
  let collapsed = false;
  try { collapsed = localStorage.getItem("engineering-manager-sidebar-collapsed") === "true"; } catch { collapsed = false; }
  if (collapsed && !mobile()) document.body.classList.add("sidebar-collapsed");
  toggle.addEventListener("click", () => {
    if (mobile()) {
      const open = sidebar.classList.toggle("is-open");
      toggle.setAttribute("aria-expanded", String(open));
      return;
    }
    const next = document.body.classList.toggle("sidebar-collapsed");
    toggle.setAttribute("aria-expanded", String(!next));
    try { localStorage.setItem("engineering-manager-sidebar-collapsed", String(next)); } catch { /* storage unavailable */ }
  });
  sidebar.querySelectorAll("a").forEach((link) => link.addEventListener("click", () => {
    if (mobile()) sidebar.classList.remove("is-open");
  }));
}

function initNetworkStatus() {
  const target = document.querySelector("[data-network-status]");
  if (!target) return;
  const update = () => {
    target.textContent = navigator.onLine ? "在线" : "离线";
    target.classList.toggle("is-offline", !navigator.onLine);
  };
  window.addEventListener("online", update);
  window.addEventListener("offline", update);
  update();
}

export async function initShell() {
  initSidebar();
  initNetworkStatus();
}
