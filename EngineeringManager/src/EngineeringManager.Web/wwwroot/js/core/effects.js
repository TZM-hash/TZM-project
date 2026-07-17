function initNumberReveal() {
  if (document.body.classList.contains("ui-effects-low") || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  document.querySelectorAll(".metric-value").forEach((element) => {
    const text = element.textContent.trim().replaceAll(",", "");
    if (!/^[-+]?\d+(\.\d+)?$/.test(text)) return;
    const target = Number(text);
    const decimals = text.includes(".") ? text.split(".")[1].length : 0;
    const started = performance.now();
    const draw = (now) => {
      const progress = Math.min((now - started) / 520, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      element.textContent = (target * eased).toLocaleString("zh-CN", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
      if (progress < 1) requestAnimationFrame(draw);
    };
    requestAnimationFrame(draw);
  });
}

function initClickFeedback() {
  if (!document.body.classList.contains("ui-effects-high") || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  document.addEventListener("pointerdown", (event) => {
    if (!event.target.closest(".button, .nav-link, .option-card")) return;
    const ripple = document.createElement("span");
    ripple.className = "ui-click-ripple";
    ripple.style.left = `${event.clientX}px`;
    ripple.style.top = `${event.clientY}px`;
    document.body.appendChild(ripple);
    ripple.addEventListener("animationend", () => ripple.remove(), { once: true });
  });
}

export async function initEffects() {
  initNumberReveal();
  initClickFeedback();
}
