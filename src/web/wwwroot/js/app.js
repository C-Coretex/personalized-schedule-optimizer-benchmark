document.addEventListener('DOMContentLoaded', async () => {
  const output = document.getElementById('forecast-output');

  try {
    const res = await fetch('/weatherforecast');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();

    const table = document.createElement('table');
    table.innerHTML = `
      <thead>
        <tr><th>Date</th><th>Temp (C)</th><th>Temp (F)</th><th>Summary</th></tr>
      </thead>
      <tbody>
        ${data.map(d => `
          <tr>
            <td>${d.date}</td>
            <td>${d.temperatureC}</td>
            <td>${d.temperatureF}</td>
            <td>${d.summary}</td>
          </tr>
        `).join('')}
      </tbody>
    `;
    output.replaceChildren(table);
  } catch (err) {
    output.textContent = `Error: ${err.message}`;
  }
});
