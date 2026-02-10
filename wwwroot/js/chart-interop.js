// Chart.js interop for Blazor Supercompensation App
let chartInstance = null;

window.renderSupercompChart = function (days, performance, baselines, phases, sprintBoundaries, showPhases, showBaseline, sprintDuration) {
    const ctx = document.getElementById('supercompChart');
    if (!ctx) return;

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
            'Fatigue': 'rgba(239, 68, 68, 0.9)',       // red
            'Recovery': 'rgba(245, 158, 11, 0.9)',      // amber
            'Supercompensation': 'rgba(16, 185, 129, 0.9)' // green
        };

        // Create segments with per-point coloring
        const pointColors = phases.map(p => phaseColors[p] || 'rgba(59, 130, 246, 0.9)');

        datasets.push({
            label: 'Wydajność zespołu',
            data: performance,
            borderColor: function (context) {
                const index = context.dataIndex;
                return pointColors[index] || 'rgba(59, 130, 246, 0.9)';
            },
            segment: {
                borderColor: function (ctx) {
                    const idx = ctx.p0DataIndex;
                    return pointColors[idx] || 'rgba(59, 130, 246, 0.9)';
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
            borderColor: 'rgba(59, 130, 246, 1)',
            backgroundColor: 'rgba(59, 130, 246, 0.1)',
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
            borderColor: 'rgba(148, 163, 184, 0.8)',
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
            borderColor: 'rgba(100, 116, 139, 0.4)',
            borderWidth: 1,
            borderDash: [4, 4],
            label: {
                content: 'Sprint ' + (i + 2),
                enabled: true,
                position: 'start',
                backgroundColor: 'rgba(100, 116, 139, 0.7)',
                color: '#fff',
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
                    backgroundColor: 'rgba(15, 23, 42, 0.95)',
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
                    color: '#1e293b'
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Dzień',
                        font: { size: 14 },
                        color: '#475569'
                    },
                    grid: {
                        color: 'rgba(148, 163, 184, 0.15)'
                    },
                    ticks: {
                        maxTicksLimit: 20,
                        color: '#64748b'
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: 'Wydajność zespołu (ważona)',
                        font: { size: 14 },
                        color: '#475569'
                    },
                    grid: {
                        color: 'rgba(148, 163, 184, 0.15)'
                    },
                    ticks: {
                        color: '#64748b'
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
                        'rgba(239, 68, 68, 0.05)');

                    // Recovery phase (50-80%)
                    drawPhaseRect(ctx, xAxis, chartArea,
                        sprintStart + sprintDuration * 0.5,
                        sprintStart + sprintDuration * 0.8,
                        'rgba(245, 158, 11, 0.05)');

                    // Supercompensation phase (80-100%)
                    drawPhaseRect(ctx, xAxis, chartArea,
                        sprintStart + sprintDuration * 0.8,
                        sprintStart + sprintDuration,
                        'rgba(16, 185, 129, 0.08)');
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
