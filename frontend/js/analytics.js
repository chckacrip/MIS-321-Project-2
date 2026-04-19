import { Chart, registerables } from 'https://cdn.jsdelivr.net/npm/chart.js@4/+esm';
Chart.register(...registerables);

const METRICS = [
  { value: 'revenue',    label: 'Revenue'     },
  { value: 'fuel',       label: 'Fuel Spend'  },
  { value: 'load_count', label: 'Load Count'  },
];

const GROUP_BYS = [
  { value: 'driver', label: 'Driver' },
  { value: 'route',  label: 'Route'  },
  { value: 'month',  label: 'Month'  },
  { value: 'status', label: 'Status' },
];

const STATUS_COLORS = {
  pending:  '#f59e0b',
  complete: '#10b981',
  invoiced: '#3b82f6',
  paid:     '#8b5cf6',
};

const CHART_DEFAULTS = {
  font:        { family: "'Inter', system-ui, sans-serif", size: 12 },
  color:       '#94a3b8',
  borderColor: '#e2e8f0',
  gridColor:   '#f1f5f9',
};

let chartInstance = null;
const chatHistory = [
  { role: 'assistant', html: 'Hi! Ask me anything about your loads, drivers, revenue, or fuel spend.' },
];

export async function renderAnalytics(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Analytics</h2>
      </div>

      <div class="chart-card chart-explorer">
        <div class="chart-controls">
          <div class="control-group">
            <label>Metric</label>
            <select id="select-metric">
              ${METRICS.map(m => `<option value="${m.value}">${m.label}</option>`).join('')}
            </select>
          </div>
          <div class="control-group">
            <label>Group By</label>
            <select id="select-groupby">
              ${GROUP_BYS.map(g => `<option value="${g.value}">${g.label}</option>`).join('')}
            </select>
          </div>
        </div>
        <div class="chart-canvas-wrap">
          <canvas id="main-chart"></canvas>
        </div>
      </div>

      <div class="chat-section">
        <div class="chat-section-header">
          <h3>AI Assistant</h3>
          <span class="chat-subtitle">Ask plain-English questions about your data</span>
        </div>
        <div class="chat-messages" id="chat-messages"></div>
        <div class="chat-input-row">
          <input
            type="text"
            id="chat-input"
            placeholder='e.g. "Who is the top earning driver?" or "What routes have the highest fuel spend?"'
          />
          <button class="btn-primary" id="chat-send">Send</button>
        </div>
      </div>
    </div>
  `;

  const metricEl  = document.getElementById('select-metric');
  const groupByEl = document.getElementById('select-groupby');

  async function refresh() {
    await updateChart(metricEl.value, groupByEl.value);
  }

  metricEl.addEventListener('change', refresh);
  groupByEl.addEventListener('change', refresh);

  await refresh();
  setupChat();
  restoreChatHistory();
}

async function updateChart(metric, groupBy) {
  const res  = await fetch(`/api/analytics/query?metric=${metric}&groupBy=${groupBy}`);
  const data = await res.json();

  if (!Array.isArray(data) || data.length === 0) {
    if (chartInstance) { chartInstance.destroy(); chartInstance = null; }
    return;
  }

  const labels = data.map(r => r.label);
  const values = data.map(r => r.value);

  const isDoughnut  = groupBy === 'status';
  const isLine      = groupBy === 'month';
  const isHorizBar  = groupBy === 'route';
  const isCurrency  = metric === 'revenue' || metric === 'fuel';

  const type = isDoughnut ? 'doughnut' : isLine ? 'line' : 'bar';

  const primaryColor = '#ea580c';
  const primaryAlpha = 'rgba(234,88,12,0.1)';

  const bgColors = isDoughnut
    ? labels.map(l => STATUS_COLORS[l] ?? '#94a3b8')
    : isLine ? primaryColor : primaryColor;

  const dataset = {
    data: values,
    backgroundColor: isDoughnut ? bgColors : isLine ? primaryAlpha : primaryColor,
    borderRadius: isDoughnut ? 0 : 5,
    borderSkipped: false,
    ...(isLine ? {
      borderColor: primaryColor,
      borderWidth: 2,
      fill: true,
      tension: 0.35,
      pointRadius: 4,
      pointBackgroundColor: primaryColor,
      pointBorderColor: '#fff',
      pointBorderWidth: 2,
    } : {}),
  };

  const dollarTick = v => '$' + Number(v).toLocaleString();

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: isDoughnut,
        position: 'right',
        labels: {
          font: CHART_DEFAULTS.font,
          color: CHART_DEFAULTS.color,
          boxWidth: 12,
          padding: 16,
        },
      },
      tooltip: {
        backgroundColor: '#1e293b',
        titleColor: '#f1f5f9',
        bodyColor: '#94a3b8',
        padding: 10,
        cornerRadius: 7,
        callbacks: {
          label: ctx => isCurrency
            ? ' $' + Number(ctx.parsed[isHorizBar ? 'x' : 'y']).toLocaleString('en-US', { minimumFractionDigits: 2 })
            : ' ' + ctx.parsed[isHorizBar ? 'x' : 'y'],
        },
      },
    },
    ...(isDoughnut ? { cutout: '62%' } : {
      indexAxis: isHorizBar ? 'y' : 'x',
      scales: {
        x: {
          grid: { color: isHorizBar ? CHART_DEFAULTS.gridColor : 'transparent' },
          border: { display: false },
          ticks: {
            font: CHART_DEFAULTS.font,
            color: CHART_DEFAULTS.color,
            callback: (isHorizBar && isCurrency) ? dollarTick : v => v,
          },
        },
        y: {
          grid: { color: isHorizBar ? 'transparent' : CHART_DEFAULTS.gridColor },
          border: { display: false },
          ticks: {
            font: CHART_DEFAULTS.font,
            color: CHART_DEFAULTS.color,
            callback: (!isHorizBar && isCurrency) ? dollarTick : v => v,
          },
        },
      },
    }),
  };

  if (chartInstance) chartInstance.destroy();

  chartInstance = new Chart(document.getElementById('main-chart'), {
    type,
    data: { labels, datasets: [dataset] },
    options,
  });
}

function setupChat() {
  const messagesEl = document.getElementById('chat-messages');
  const inputEl    = document.getElementById('chat-input');
  const sendBtn    = document.getElementById('chat-send');

  async function sendMessage() {
    const message = inputEl.value.trim();
    if (!message) return;

    inputEl.value = '';
    appendMessage('user', message);

    const thinking = appendMessage('assistant', '...');
    sendBtn.disabled = true;

    try {
      const res  = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message }),
      });
      const data = await res.json();

      thinking.innerHTML = '';

      const replyText = document.createElement('span');
      replyText.textContent = data.reply;
      thinking.appendChild(replyText);

      if (data.sql) {
        const details = document.createElement('details');
        details.className = 'sql-details';
        details.innerHTML = `<summary>View SQL</summary><pre>${data.sql}</pre>`;
        thinking.appendChild(details);
      }
    } catch {
      thinking.textContent = 'Something went wrong. Please try again.';
    } finally {
      sendBtn.disabled = false;
      inputEl.focus();
    }
  }

  function appendMessage(role, text) {
    const el = document.createElement('div');
    el.className = `chat-message chat-message-${role}`;
    el.textContent = text;
    messagesEl.appendChild(el);
    messagesEl.scrollTop = messagesEl.scrollHeight;
    return el;
  }

  sendBtn.addEventListener('click', sendMessage);
  inputEl.addEventListener('keydown', e => { if (e.key === 'Enter') sendMessage(); });
}
