const form = document.querySelector("#generateForm");
const generateButton = document.querySelector("#generateButton");
const resetButton = document.querySelector("#resetButton");
const statusPill = document.querySelector("#statusPill");
const stageLabel = document.querySelector("#stageLabel");
const messageLabel = document.querySelector("#messageLabel");
const percentLabel = document.querySelector("#percentLabel");
const progressBar = document.querySelector("#progressBar");
const fileMetric = document.querySelector("#fileMetric");
const outputMetric = document.querySelector("#outputMetric");
const logOutput = document.querySelector("#logOutput");
const mermaidOutput = document.querySelector("#mermaidOutput");

let pollHandle = null;

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  stopPolling();

  const data = new FormData(form);
  const payload = {
    projectFolder: data.get("projectFolder")?.trim() ?? "",
    searchFolder: data.get("searchFolder")?.trim() ?? "",
    searchFile: data.get("searchFile")?.trim() || null,
    outputPath: data.get("outputPath")?.trim() ?? ""
  };

  setBusy(true);
  setSnapshot({
    status: "Queued",
    stage: "Queued",
    message: "ジョブを作成しています...",
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: []
  });
  mermaidOutput.value = "";

  try {
    const response = await fetch("/api/generate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }

    const result = await response.json();
    pollJob(result.id);
  } catch (error) {
    setBusy(false);
    setFailed(error.message);
  }
});

resetButton.addEventListener("click", () => {
  stopPolling();
  form.reset();
  setBusy(false);
  setSnapshot({
    status: "Queued",
    stage: "待機中",
    message: "入力して生成を開始してください。",
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: []
  });
  mermaidOutput.value = "";
});

async function pollJob(id) {
  const update = async () => {
    try {
      const response = await fetch(`/api/jobs/${id}`);
      if (!response.ok) {
        throw new Error(await response.text());
      }

      const snapshot = await response.json();
      setSnapshot(snapshot);

      if (snapshot.status === "Completed") {
        stopPolling();
        setBusy(false);
        mermaidOutput.value = snapshot.mermaid ?? "";
      } else if (snapshot.status === "Failed") {
        stopPolling();
        setBusy(false);
      }
    } catch (error) {
      stopPolling();
      setBusy(false);
      setFailed(error.message);
    }
  };

  await update();
  pollHandle = window.setInterval(update, 600);
}

function stopPolling() {
  if (pollHandle !== null) {
    window.clearInterval(pollHandle);
    pollHandle = null;
  }
}

function setBusy(isBusy) {
  generateButton.disabled = isBusy;
  generateButton.textContent = isBusy ? "処理中" : "生成";
}

function setFailed(message) {
  setSnapshot({
    status: "Failed",
    stage: "Failed",
    message,
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: [message]
  });
}

function setSnapshot(snapshot) {
  const percent = clamp(Number(snapshot.percent ?? 0), 0, 100);
  const status = snapshot.status ?? "Queued";

  stageLabel.textContent = snapshot.stage ?? status;
  messageLabel.textContent = snapshot.message ?? "";
  percentLabel.textContent = `${percent}%`;
  progressBar.style.width = `${percent}%`;
  fileMetric.textContent = `${snapshot.processedFiles ?? 0} / ${snapshot.totalFiles ?? 0} files`;
  outputMetric.textContent = snapshot.outputPath ? `出力: ${snapshot.outputPath}` : "出力なし";
  logOutput.textContent = Array.isArray(snapshot.log) ? snapshot.log.join("\n") : "";

  statusPill.className = "status-pill";
  if (status === "Running") {
    statusPill.classList.add("running");
    statusPill.textContent = "処理中";
  } else if (status === "Completed") {
    statusPill.classList.add("running");
    statusPill.textContent = "完了";
  } else if (status === "Failed") {
    statusPill.classList.add("failed");
    statusPill.textContent = "失敗";
  } else {
    statusPill.textContent = "待機中";
  }
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}
