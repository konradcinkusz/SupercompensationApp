// Chart.js interop for Blazor Supercompensation App

// ─────────────────────────────────────────────────────────────────────────────
// Colours come from the CSS custom properties in wwwroot/css/app.css, never from
// literals here.
//
// This file used to carry its own copy of the palette, and the copy had drifted: the
// chart title was painted '#1e293b' while .chart-container's background is
// var(--bg-card), which is ALSO #1e293b. Contrast ratio 1.00:1 — the heading of the
// application's central visual, drawn in its own background colour. The axis titles
// (#475569, 1.93:1) and tick labels (#64748b, 3.07:1) were the same mistake in milder
// form: all of them were picked for a light background, and the chart is the one
// surface that was never converted when the app went dark.
//
// There is one palette. A second copy of it in a different file is what produced that
// defect, so the fix is to stop having a second copy rather than to correct it.
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Reads a CSS custom property off :root. Returns '' if the stylesheet has not loaded.
 */
function cssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/**
 * Applies an alpha channel to a #rrggbb value, so the palette can be reused at the
 * opacities this chart wants (grid lines, phase bands, the tooltip ground) without
 * re-encoding the same colour as an rgba() literal.
 */
function withAlpha(hex, alpha) {
    const value = String(hex).replace('#', '');
    const r = parseInt(value.substring(0, 2), 16);
    const g = parseInt(value.substring(2, 4), 16);
    const b = parseInt(value.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

/**
 * The palette, read once per render.
 *
 * FALLBACK is the single literal left in this file, and it is deliberately NOT a copy
 * of any palette value: it is a last-resort readable-on-dark colour used only if
 * app.css failed to load, in which case the page has larger problems. Making it a
 * distinct value means a stylesheet that did not load looks wrong rather than looking
 * fine, which is the direction a failsafe should fail in.
 */
function readPalette() {
    const FALLBACK = '#ffffff';
    const read = (name) => cssVar(name) || FALLBACK;
    return {
        textPrimary: read('--text-primary'),
        textSecondary: read('--text-secondary'),
        textMuted: read('--text-muted'),
        bgPrimary: read('--bg-primary'),
        blue: read('--accent-blue'),
        red: read('--accent-red'),
        amber: read('--accent-amber'),
        green: read('--accent-green'),
    };
}

let chartInstance = null;

window.renderSupercompChart = function (days, performance, baselines, phases, sprintBoundaries, showPhases, showBaseline, sprintDuration) {
    const ctx = document.getElementById('supercompChart');
    if (!ctx) return;

    const palette = readPalette();

    // Chart.js defaults its text to '#666', which is 2.55:1 on --bg-card and fails
    // WCAG AA. That is why the LEGEND was unreadable too — it sets no colour of its
    // own, so it inherited the library default. Setting the default rather than
    // enumerating every element means anything added later inherits a legible colour
    // instead of quietly reintroducing this bug.
    Chart.defaults.color = palette.textSecondary;

    // Destroy existing chart
    if (chartInstance) {
        chartInstance.destroy();
        chartInstance = null;
    }

    // Build datasets
    const datasets = [];

    // Performance line
    if (showPhases) {
        // Split performance by phase with colors
        const phaseColors = {
            'Fatigue': withAlpha(palette.red, 0.9),       // red
            'Recovery': withAlpha(palette.amber, 0.9),      // amber
            'Supercompensation': withAlpha(palette.green, 0.9) // green
        };

        // Create segments with per-point coloring
        const pointColors = phases.map(p => phaseColors[p] || withAlpha(palette.blue, 0.9));

        datasets.push({
            label: 'Wydajność zespołu',
            data: performance,
            borderColor: function (context) {
                const index = context.dataIndex;
                return pointColors[index] || withAlpha(palette.blue, 0.9);
            },
            segment: {
                borderColor: function (ctx) {
                    const idx = ctx.p0DataIndex;
                    return pointColors[idx] || withAlpha(palette.blue, 0.9);
                }
            },
            backgroundColor: 'transparent',
            borderWidth: 3,
            pointRadius: 0,
            tension: 0.4,
            fill: false,
            order: 1
        });
    } else {
        datasets.push({
            label: 'Wydajność zespołu',
            data: performance,
            borderColor: palette.blue,
            backgroundColor: withAlpha(palette.blue, 0.1),
            borderWidth: 3,
            pointRadius: 0,
            tension: 0.4,
            fill: false,
            order: 1
        });
    }

    // Baseline line(s)
    if (showBaseline) {
        datasets.push({
            label: 'Baseline',
            data: baselines,
            borderColor: withAlpha(palette.textSecondary, 0.8),
            backgroundColor: 'transparent',
            borderWidth: 2,
            borderDash: [8, 4],
            pointRadius: 0,
            tension: 0,
            fill: false,
            order: 2
        });
    }

    // Sprint boundary annotations (vertical lines via plugin)
    const annotations = {};
    sprintBoundaries.forEach((boundary, i) => {
        annotations['sprint' + i] = {
            type: 'line',
            xMin: boundary,
            xMax: boundary,
            borderColor: withAlpha(palette.textMuted, 0.4),
            borderWidth: 1,
            borderDash: [4, 4],
            label: {
                content: 'Sprint ' + (i + 2),
                enabled: true,
                position: 'start',
                backgroundColor: withAlpha(palette.textMuted, 0.7),
                color: palette.textPrimary,
                font: { size: 11 }
            }
        };
    });

    chartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: days,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false,
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        usePointStyle: true,
                        padding: 20,
                        font: { size: 13 }
                    }
                },
                tooltip: {
                    backgroundColor: withAlpha(palette.bgPrimary, 0.95),
                    titleFont: { size: 13 },
                    bodyFont: { size: 12 },
                    padding: 12,
                    cornerRadius: 8,
                    callbacks: {
                        title: function (items) {
                            const day = items[0].label;
                            const sprint = Math.floor(day / sprintDuration) + 1;
                            const dayInSprint = (day % sprintDuration) + 1;
                            return `Dzień ${day} (Sprint ${sprint}, dzień ${dayInSprint})`;
                        },
                        label: function (item) {
                            return `${item.dataset.label}: ${item.formattedValue}`;
                        }
                    }
                },
                annotation: {
                    annotations: annotations
                },
                title: {
                    display: true,
                    text: 'Model Hiperkompensacji — Wydajność Zespołu',
                    font: { size: 18, weight: 'bold' },
                    padding: { bottom: 20 },
                    color: palette.textPrimary
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Dzień',
                        font: { size: 14 },
                        color: palette.textSecondary
                    },
                    grid: {
                        color: withAlpha(palette.textSecondary, 0.15)
                    },
                    ticks: {
                        maxTicksLimit: 20,
                        color: palette.textSecondary
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: 'Wydajność zespołu (ważona)',
                        font: { size: 14 },
                        color: palette.textSecondary
                    },
                    grid: {
                        color: withAlpha(palette.textSecondary, 0.15)
                    },
                    ticks: {
                        color: palette.textSecondary
                    }
                }
            }
        },
        plugins: [{
            id: 'phaseBackground',
            beforeDraw: function (chart) {
                if (!showPhases) return;

                const ctx = chart.ctx;
                const xAxis = chart.scales.x;
                const yAxis = chart.scales.y;
                const chartArea = chart.chartArea;

                // Draw colored backgrounds for each sprint's phases
                const totalDays = days.length > 0 ? days[days.length - 1] : 0;
                const numSprints = Math.ceil(totalDays / sprintDuration);

                for (let s = 0; s < numSprints; s++) {
                    const sprintStart = s * sprintDuration;

                    // Fatigue phase (0-50%)
                    drawPhaseRect(ctx, xAxis, chartArea,
                        sprintStart, sprintStart + sprintDuration * 0.5,
                        withAlpha(palette.red, 0.05));

                    // Recovery phase (50-80%)
                    drawPhaseRect(ctx, xAxis, chartArea,
                        sprintStart + sprintDuration * 0.5,
                        sprintStart + sprintDuration * 0.8,
                        withAlpha(palette.amber, 0.05));

                    // Supercompensation phase (80-100%)
                    drawPhaseRect(ctx, xAxis, chartArea,
                        sprintStart + sprintDuration * 0.8,
                        sprintStart + sprintDuration,
                        withAlpha(palette.green, 0.08));
                }
            }
        }]
    });

    function drawPhaseRect(ctx, xAxis, chartArea, x1, x2, color) {
        const left = xAxis.getPixelForValue(x1);
        const right = xAxis.getPixelForValue(x2);
        ctx.save();
        ctx.fillStyle = color;
        ctx.fillRect(left, chartArea.top, right - left, chartArea.bottom - chartArea.top);
        ctx.restore();
    }
};

// CSV download helper
window.downloadCsv = function (csvContent, fileName) {
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', fileName);
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};
