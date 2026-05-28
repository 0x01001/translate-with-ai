// State
let settings = {
    geminiKey: "",
    geminiModel: "gemini-1.5-flash",
    openaiKey: "",
    openaiModel: "gpt-4o-mini",
    activeProvider: "gemini",
    autostart: true
};

const hotkeyInput = document.getElementById("hotkey-input");
const hotkeyError = document.getElementById("hotkey-error");

document.addEventListener("DOMContentLoaded", () => {
    refreshAppSettings();
    
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "ui_ready" });
    }

    // Prevent zoom shortcuts
    window.addEventListener('keydown', (e) => {
        if (e.ctrlKey && (e.key === '=' || e.key === '-' || e.key === '0' || e.key === '+' || e.key === '_')) {
            e.preventDefault();
        }
    }, { passive: false });

    window.addEventListener('wheel', (e) => {
        if (e.ctrlKey) e.preventDefault();
    }, { passive: false });
});

// Load Settings from LocalStorage
function refreshAppSettings() {
    const saved = localStorage.getItem("rewrite_settings") || localStorage.getItem("ai_portal_settings");

    if (saved) {
        try {
            settings = {
                ...settings,
                ...JSON.parse(saved)
            };
        } catch (e) {
            console.error("Failed to parse settings", e);
        }
    }

    // Sync UI from settings
    document.getElementById("gemini-key").value = settings.geminiKey || "";
    document.getElementById("gemini-model").value = settings.geminiModel || "";
    document.getElementById("openai-key").value = settings.openaiKey || "";
    document.getElementById("openai-model").value = settings.openaiModel || "";
    document.getElementById("active-provider").value = settings.activeProvider || "gemini";

    // Ask C# runtime states
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "get_startup" });
        window.chrome.webview.postMessage({ action: "get_hotkey" });
    }
}

// Save Settings to LocalStorage and C#
function saveSettings() {
    settings.geminiKey = document.getElementById("gemini-key").value.trim();
    settings.geminiModel = document.getElementById("gemini-model").value.trim();
    settings.openaiKey = document.getElementById("openai-key").value.trim();
    settings.openaiModel = document.getElementById("openai-model").value.trim();
    settings.activeProvider = document.getElementById("active-provider").value;
    
    localStorage.setItem("rewrite_settings", JSON.stringify(settings));
    
    // Toggle autostart in Windows via C#
    const isAutostartChecked = document.getElementById("system-startup").checked;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ 
            action: "set_startup", 
            enabled: isAutostartChecked 
        });
        if (hotkeyInput) {
            if (hotkeyError) hotkeyError.textContent = "";
            window.chrome.webview.postMessage({
                action: "set_hotkey",
                hotkey: hotkeyInput.value.trim()
            });
        }
    }

    showToast();
}

function showToast() {
    const toast = document.getElementById("settings-toast");
    toast.classList.remove("opacity-0");
    toast.classList.add("opacity-100");
    setTimeout(() => {
        toast.classList.remove("opacity-100");
        toast.classList.add("opacity-0");
    }, 2000);
}

// Setup WebView2 Bridge listener for states from C#
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", event => {
        const data = event.data;
        
        if (data.event === "startup_status") {
            document.getElementById("system-startup").checked = data.enabled;
        }
        else if (data.event === "hotkey_status") {
            if (hotkeyInput) hotkeyInput.value = data.hotkey || "Ctrl+Shift+A";
        }
        else if (data.event === "hotkey_error") {
            if (hotkeyError) {
                hotkeyError.textContent = data.message || "Phím tắt không hợp lệ.";
            }
        }
    });
}

// Settings Save Trigger
document.getElementById("save-settings-btn").addEventListener("click", saveSettings);

// Record Hotkey
if (hotkeyInput) {
    hotkeyInput.addEventListener("keydown", event => {
        event.preventDefault();
        event.stopPropagation();

        const key = event.key;
        if (key === "Control" || key === "Shift" || key === "Alt" || key === "Meta") {
            let mods = [];
            if (event.ctrlKey) mods.push("Ctrl");
            if (event.shiftKey) mods.push("Shift");
            if (event.altKey) mods.push("Alt");
            if (event.metaKey) mods.push("Win");
            
            if (mods.length > 0) {
                hotkeyInput.value = mods.join("+") + "+...";
            }
            return;
        }

        let parts = [];
        if (event.ctrlKey) parts.push("Ctrl");
        if (event.shiftKey) parts.push("Shift");
        if (event.altKey) parts.push("Alt");
        if (event.metaKey) parts.push("Win");

        let keyDisplay = key;
        if (key === " ") {
            keyDisplay = "Space";
        } else if (key === "Escape") {
            keyDisplay = "Esc";
        } else if (key.length === 1) {
            keyDisplay = key.toUpperCase();
        } else {
            keyDisplay = key.charAt(0).toUpperCase() + key.slice(1);
            if (keyDisplay === "ArrowUp") keyDisplay = "Up";
            if (keyDisplay === "ArrowDown") keyDisplay = "Down";
            if (keyDisplay === "ArrowLeft") keyDisplay = "Left";
            if (keyDisplay === "ArrowRight") keyDisplay = "Right";
        }

        parts.push(keyDisplay);
        hotkeyInput.value = parts.join("+");
    });
}

// Drag capability
document.addEventListener("mousedown", event => {
    const target = event.target;
    if (!target || target.closest("button, input, select, textarea, a, svg, path")) return;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "start_drag" });
    }
});
