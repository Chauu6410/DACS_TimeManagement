// =======================
// 🍅 Pomodoro Timer Script
// =======================

// Cấu hình
const DEFAULT_TIME = 25 * 60; // 25 phút

let totalTime = DEFAULT_TIME;
let currentTime = DEFAULT_TIME;
let timerInterval = null;
let isRunning = false;

// =======================
// 🚀 INIT
// =======================
document.addEventListener("DOMContentLoaded", function () {

    // =======================
    // 🖱️ DRAGGABLE (IMPROVED)
    // =======================
    function makeDraggable(element) {
        let isDragging = false;
        let startX, startY;
        let currentX = 0, currentY = 0;

        // Load vị trí cũ
        const savedPos = JSON.parse(localStorage.getItem("pomodoroPos"));
        if (savedPos) {
            currentX = savedPos.x || 0;
            currentY = savedPos.y || 0;
            setTranslate(currentX, currentY, element);
        }

        const handle = document.getElementById(element.id + "Header") || element;

        // Mouse events
        handle.addEventListener("mousedown", dragStart);
        document.addEventListener("mousemove", dragMove);
        document.addEventListener("mouseup", dragEnd);

        // Touch events (mobile)
        handle.addEventListener("touchstart", dragStart);
        document.addEventListener("touchmove", dragMove);
        document.addEventListener("touchend", dragEnd);

        function dragStart(e) {
            isDragging = true;

            const event = e.type.includes("touch") ? e.touches[0] : e;

            startX = event.clientX - currentX;
            startY = event.clientY - currentY;

            element.style.transition = "none"; // bỏ animation khi kéo
        }

        function dragMove(e) {
            if (!isDragging) return;

            const event = e.type.includes("touch") ? e.touches[0] : e;

            e.preventDefault();

            currentX = event.clientX - startX;
            currentY = event.clientY - startY;

            // 🔒 Giới hạn trong màn hình
            const rect = element.getBoundingClientRect();
            const maxX = window.innerWidth - rect.width;
            const maxY = window.innerHeight - rect.height;

            currentX = Math.max(0, Math.min(currentX, maxX));
            currentY = Math.max(0, Math.min(currentY, maxY));

            setTranslate(currentX, currentY, element);
        }

        function dragEnd() {
            if (!isDragging) return;

            isDragging = false;

            element.style.transition = "transform 0.2s ease"; // mượt hơn

            // Lưu vị trí
            localStorage.setItem("pomodoroPos", JSON.stringify({
                x: currentX,
                y: currentY
            }));
        }

        function setTranslate(x, y, el) {
            el.style.transform = `translate(${x}px, ${y}px)`;
        }
    }

    // Khởi tạo tính năng kéo cho widget
    const widget = document.getElementById("pomodoroWidget");
    if (widget) {
        makeDraggable(widget);

        // Load lại vị trí cũ nếu có
        const savedPos = JSON.parse(localStorage.getItem("pomodoroPos"));
        if (savedPos) {
            widget.style.top = savedPos.top;
            widget.style.left = savedPos.left;
        }
    }
    // Bind events
    document.getElementById("startBtn")?.addEventListener("click", startTimer);
    document.getElementById("pauseBtn")?.addEventListener("click", pauseTimer);
    document.getElementById("resetBtn")?.addEventListener("click", resetTimer);
    document.getElementById("togglePomodoro")?.addEventListener("click", toggleWidget);

    // Load state từ localStorage
    loadState();

    updateDisplay();
});

// =======================
// 🎛️ CONTROL
// =======================

// Start
function startTimer() {
    if (isRunning) return;

    isRunning = true;

    timerInterval = setInterval(() => {
        if (currentTime > 0) {
            currentTime--;
            updateDisplay();
            saveState();
        } else {
            stopTimer();
            notifyUser();
        }
    }, 1000);
}

// Pause
function pauseTimer() {
    stopTimer();
}

// Reset
function resetTimer() {
    stopTimer();
    currentTime = totalTime;
    updateDisplay();
    saveState();
}

// Stop helper
function stopTimer() {
    clearInterval(timerInterval);
    timerInterval = null;
    isRunning = false;
}

// =======================
// 🎨 UI
// =======================

// Update UI
function updateDisplay() {
    let minutes = Math.floor(currentTime / 60);
    let seconds = currentTime % 60;

    const display = document.getElementById("timerDisplay");
    if (display) {
        display.innerText =
            `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }

    // Progress bar
    const progress = document.getElementById("progressBar");
    if (progress) {
        let percent = (currentTime / totalTime) * 100;
        progress.style.width = percent + "%";
    }
}

// Toggle widget
function toggleWidget() {
    const widget = document.getElementById("pomodoroWidget");
    if (!widget) return;

    if (widget.style.display === "none" || widget.style.display === "") {
        widget.style.display = "block";
    } else {
        widget.style.display = "none";
    }
}

// =======================
// 🔔 NOTIFICATION
// =======================

function notifyUser() {
    // Âm thanh
    let audio = new Audio("https://www.soundjay.com/buttons/sounds/beep-07.mp3");
    audio.play();

    // Notification
    if (Notification.permission === "granted") {
        new Notification("⏰ Hết giờ Pomodoro!", {
            body: "Đã 25 phút làm việc. Nghỉ ngơi thôi!",
        });
    } else if (Notification.permission !== "denied") {
        Notification.requestPermission();
    }
}

// =======================
// 💾 LOCAL STORAGE
// =======================

// Lưu trạng thái
function saveState() {
    const data = {
        currentTime: currentTime,
        isRunning: isRunning,
        timestamp: Date.now()
    };

    localStorage.setItem("pomodoroState", JSON.stringify(data));
}

// Load trạng thái
function loadState() {
    const data = localStorage.getItem("pomodoroState");

    if (!data) return;

    const state = JSON.parse(data);

    currentTime = state.currentTime || DEFAULT_TIME;

    // Nếu đang chạy trước đó → tính lại thời gian đã trôi qua
    if (state.isRunning) {
        const elapsed = Math.floor((Date.now() - state.timestamp) / 1000);
        currentTime = Math.max(0, currentTime - elapsed);

        if (currentTime > 0) {
            startTimer();
        } else {
            notifyUser();
        }
    }
}