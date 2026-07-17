const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const palette = ["#2563eb", "#0d9488", "#f59e0b", "#7c3aed"];

function readValue(item, key) {
  return Number(item[key] ?? item[key[0].toUpperCase() + key.slice(1)] ?? 0);
}

function clear(host) {
  host.replaceChildren();
}

function empty(host) {
  clear(host);
  const state = document.createElement("div");
  state.className = "chart-empty-state";
  state.dataset.chartEmpty = "";
  state.innerHTML = "<strong>暂无可展示数据</strong><span>录入业务数据后自动生成图形摘要。</span>";
  host.appendChild(state);
}

function canvasSize(host) {
  return { width: Math.max(host.clientWidth, 520), height: Math.max(host.clientHeight, 210) };
}

export function renderLineChart(host, points) {
  if (!Array.isArray(points) || points.length === 0) { empty(host); return; }
  clear(host);
  const { width, height } = canvasSize(host);
  const ratio = window.devicePixelRatio || 1;
  const canvas = document.createElement("canvas");
  canvas.width = width * ratio;
  canvas.height = height * ratio;
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;
  canvas.setAttribute("role", "img");
  canvas.setAttribute("aria-label", "十二个月收款、付款与开票趋势");
  host.appendChild(canvas);
  const context = canvas.getContext("2d");
  context.scale(ratio, ratio);
  const padding = { left: 54, right: 20, top: 24, bottom: 36 };
  const keys = ["collected", "paid", "invoiced"];
  const max = Math.max(1, ...points.flatMap((point) => keys.map((key) => readValue(point, key))));
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  context.font = "11px system-ui, sans-serif";
  context.strokeStyle = "rgba(148,163,184,.28)";
  context.fillStyle = "#64748b";
  for (let line = 0; line <= 4; line += 1) {
    const y = padding.top + chartHeight * line / 4;
    context.beginPath(); context.moveTo(padding.left, y); context.lineTo(width - padding.right, y); context.stroke();
    context.fillText(Math.round(max * (1 - line / 4)).toLocaleString("zh-CN"), 4, y + 4);
  }
  points.forEach((point, index) => {
    if (index % 2 !== 0 && points.length > 8) return;
    const label = point.month ?? point.Month ?? "";
    const x = padding.left + chartWidth * index / Math.max(points.length - 1, 1);
    context.fillText(label.slice(5), x - 8, height - 12);
  });
  keys.forEach((key, seriesIndex) => {
    context.beginPath();
    context.lineWidth = 2.4;
    context.strokeStyle = palette[seriesIndex];
    points.forEach((point, index) => {
      const x = padding.left + chartWidth * index / Math.max(points.length - 1, 1);
      const y = padding.top + chartHeight * (1 - readValue(point, key) / max);
      if (index === 0) context.moveTo(x, y); else context.lineTo(x, y);
    });
    context.stroke();
  });
  host.dataset.motionMode = reducedMotion ? "reduced" : "standard";
}

export function renderGroupedBars(host, groups) {
  if (!Array.isArray(groups) || groups.length === 0) { empty(host); return; }
  clear(host);
  const width = Math.max(host.clientWidth, 420);
  const height = 220;
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
  svg.setAttribute("role", "img");
  const values = groups.flatMap((group) => group.values ?? group.Values ?? []);
  const max = Math.max(1, ...values.map(Number));
  const groupWidth = (width - 48) / groups.length;
  groups.forEach((group, groupIndex) => (group.values ?? group.Values ?? []).forEach((value, valueIndex) => {
    const bar = document.createElementNS(svg.namespaceURI, "rect");
    const barWidth = Math.min(22, groupWidth / 4);
    const barHeight = Number(value) / max * 160;
    bar.setAttribute("x", String(28 + groupIndex * groupWidth + valueIndex * (barWidth + 4)));
    bar.setAttribute("y", String(184 - barHeight));
    bar.setAttribute("width", String(barWidth));
    bar.setAttribute("height", String(barHeight));
    bar.setAttribute("rx", "4");
    bar.setAttribute("fill", palette[valueIndex % palette.length]);
    svg.appendChild(bar);
  }));
  host.appendChild(svg);
}

export function renderProgressRing(host, value, label = "完成率") {
  clear(host);
  const normalized = Math.min(Math.max(Number(value) || 0, 0), 100);
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 120 120");
  svg.setAttribute("role", "img");
  svg.setAttribute("aria-label", `${label} ${normalized.toFixed(1)}%`);
  svg.innerHTML = `<circle cx="60" cy="60" r="46" fill="none" stroke="#e2e8f0" stroke-width="12"/><circle cx="60" cy="60" r="46" fill="none" stroke="${palette[0]}" stroke-width="12" stroke-linecap="round" pathLength="100" stroke-dasharray="${normalized} 100" transform="rotate(-90 60 60)"/><text x="60" y="65" text-anchor="middle" font-size="20" font-weight="700" fill="#0f172a">${normalized.toFixed(0)}%</text>`;
  host.appendChild(svg);
}

function render(root) {
  const host = root.querySelector("[data-chart-canvas]") || root;
  let series = [];
  try { series = JSON.parse(root.dataset.chartSeries || "[]"); } catch { series = []; }
  if (root.dataset.chartKind === "grouped-bars") renderGroupedBars(host, series);
  else if (root.dataset.chartKind === "progress-ring") renderProgressRing(host, root.dataset.chartValue, root.dataset.chartLabel);
  else renderLineChart(host, series);
}

export function initCharts() {
  document.querySelectorAll("[data-chart]").forEach((root) => {
    render(root);
    if ("ResizeObserver" in window) new ResizeObserver(() => render(root)).observe(root);
  });
}
