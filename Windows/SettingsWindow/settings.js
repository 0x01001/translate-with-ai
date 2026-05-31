// State
let settings = {
    geminiKey: "",
    geminiModel: "gemini-1.5-flash",
    openaiKey: "",
    openaiModel: "gpt-4o-mini",
    activeProvider: "gemini",
    autostart: true,
    locale: "en"
};

let i18n = {};

const hotkeyInput = document.getElementById("hotkey-input");
const hotkeyError = document.getElementById("hotkey-error");
const activeProviderInput = document.getElementById("active-provider");
const activeProviderLabel = document.getElementById("active-provider-label");
const geminiPanel = document.getElementById("gemini-panel");
const openaiPanel = document.getElementById("openai-panel");
const activeProviderTrigger = document.getElementById("active-provider-trigger");
const activeProviderText = document.getElementById("active-provider-text");
const activeProviderMenu = document.getElementById("active-provider-menu");
const localeTrigger = document.getElementById("select-locale-trigger");
const localeText = document.getElementById("select-locale-text");
const localeMenu = document.getElementById("select-locale-menu");

document.addEventListener("DOMContentLoaded", () => {
    refreshAppSettings();
    updateProviderUI();
    bindDropdowns();
    
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "ui_ready" });
    }

    // Request locale from host
    if (window.chrome && window.chrome.webview) {
        try { window.chrome.webview.postMessage({ action: "request_locale" }); } catch { }
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
    const localeSelect = document.getElementById('select-locale');
    if (localeSelect) localeSelect.value = settings.locale || 'en';
    syncDropdownUI("provider", document.getElementById("active-provider").value);
    syncDropdownUI("locale", localeSelect ? localeSelect.value : "en");
    updateProviderUI();

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
    const localeSelect = document.getElementById('select-locale');
    if (localeSelect) settings.locale = localeSelect.value;
    
    localStorage.setItem("rewrite_settings", JSON.stringify(settings));
    updateProviderUI();
    
    // Toggle autostart in Windows via C#
    const isAutostartChecked = document.getElementById("system-startup").checked;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ 
            action: "set_startup", 
            enabled: isAutostartChecked 
        });
        // Persist locale change via host as well so all windows update
        if (localeSelect) {
            try { window.chrome.webview.postMessage({ action: 'set_locale', locale: settings.locale }); } catch { }
        }
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
    if (!toast) return;
    toast.textContent = i18n['settings.save_notice'] || i18n['settings.saved'] || toast.textContent;
    toast.style.opacity = "1";
    toast.style.transform = "translateY(0)";
    setTimeout(() => {
        toast.style.opacity = "0";
        toast.style.transform = "translateY(2px)";
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
                hotkeyError.textContent = data.message || (i18n['hotkey.invalid'] || "Phím tắt không hợp lệ.");
            }
        }
        else if (data.event === "set_locale") {
            loadLocale(data.locale);
            return;
        }
    });
}

// Settings Save Trigger
document.getElementById("save-settings-btn").addEventListener("click", saveSettings);

// Locale selector
if (activeProviderInput) {
    activeProviderInput.addEventListener("change", () => {
        settings.activeProvider = activeProviderInput.value;
        syncDropdownUI("provider", activeProviderInput.value);
        updateProviderUI();
    });
}

function setActiveProvider(provider) {
    if (activeProviderInput) {
        activeProviderInput.value = provider;
    }
    settings.activeProvider = provider;
    updateProviderUI();
}

function updateProviderUI() {
    const provider = (activeProviderInput && activeProviderInput.value) || settings.activeProvider || "gemini";
    const geminiActive = provider === "gemini";

    if (geminiPanel) geminiPanel.style.display = geminiActive ? "block" : "none";
    if (openaiPanel) openaiPanel.style.display = geminiActive ? "none" : "block";

    if (activeProviderLabel) {
        activeProviderLabel.textContent = geminiActive ? (i18n['provider.gemini'] || "Gemini") : (i18n['provider.openai'] || "OpenAI");
    }
}

function bindDropdowns() {
    setupDropdown("provider", activeProviderTrigger, activeProviderMenu, value => {
        if (activeProviderInput) activeProviderInput.value = value;
        settings.activeProvider = value;
        updateProviderUI();
    });

    setupDropdown("locale", localeTrigger, localeMenu, value => {
        const localeSelect = document.getElementById('select-locale');
        if (localeSelect) localeSelect.value = value;
        settings.locale = value;
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({ action: 'set_locale', locale: value });
        }
        syncDropdownUI("locale", value);
    });

    document.addEventListener("click", event => {
        closeDropdownIfOutside(event.target);
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closeAllDropdowns();
        }
    });
}

function setupDropdown(name, trigger, menu, onSelect) {
    if (!trigger || !menu) return;
    trigger.addEventListener("click", event => {
        event.preventDefault();
        const isOpen = trigger.getAttribute("aria-expanded") === "true";
        closeAllDropdowns();
        if (!isOpen) openDropdown(trigger, menu);
    });

    menu.querySelectorAll(".dropdown-option").forEach(option => {
        option.addEventListener("click", event => {
            event.preventDefault();
            const value = option.getAttribute("data-value") || "";
            onSelect(value);
            syncDropdownUI(name, value);
            closeAllDropdowns();
        });
    });
}

function syncDropdownUI(name, value) {
    const triggerText = name === "provider" ? activeProviderText : localeText;
    const menu = name === "provider" ? activeProviderMenu : localeMenu;
    const trigger = name === "provider" ? activeProviderTrigger : localeTrigger;
    if (!menu || !triggerText || !trigger) return;

    const option = menu.querySelector(`.dropdown-option[data-value="${CSS.escape(value)}"]`);
    if (option) {
        const label = option.getAttribute("data-label-key");
        triggerText.textContent = label && i18n[label] ? i18n[label] : option.textContent.trim();
    }

    menu.querySelectorAll(".dropdown-option").forEach(opt => {
        opt.setAttribute("aria-selected", opt.getAttribute("data-value") === value ? "true" : "false");
    });
}

function openDropdown(trigger, menu) {
    const wrapper = trigger.closest(".dropdown");
    if (!wrapper) return;
    wrapper.classList.add("open");
    trigger.setAttribute("aria-expanded", "true");
    positionDropdown(wrapper, trigger, menu);
}

function closeAllDropdowns() {
    document.querySelectorAll(".dropdown.open").forEach(dropdown => {
        dropdown.classList.remove("open");
        dropdown.classList.remove("open-up");
    });
    document.querySelectorAll(".dropdown-trigger[aria-expanded='true']").forEach(trigger => {
        trigger.setAttribute("aria-expanded", "false");
    });
    document.querySelectorAll(".dropdown-menu").forEach(menu => {
        menu.style.top = "";
        menu.style.bottom = "";
        menu.style.maxHeight = "";
    });
}

function closeDropdownIfOutside(target) {
    if (!target) return;
    if (target.closest(".dropdown")) return;
    closeAllDropdowns();
}

function positionDropdown(wrapper, trigger, menu) {
    if (!wrapper || !trigger || !menu) return;

    const rect = trigger.getBoundingClientRect();
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
    const viewportPadding = 12;
    const menuHeight = menu.scrollHeight || 260;
    const spaceBelow = viewportHeight - rect.bottom - viewportPadding;
    const spaceAbove = rect.top - viewportPadding;
    const openUp = spaceBelow < 220 && spaceAbove > spaceBelow;
    const availableSpace = Math.max(openUp ? spaceAbove : spaceBelow, 120);
    const maxHeight = Math.min(menuHeight, availableSpace);

    wrapper.classList.toggle("open-up", openUp);
    menu.style.top = openUp ? "auto" : "calc(100% + 8px)";
    menu.style.bottom = openUp ? "calc(100% + 8px)" : "auto";
    menu.style.maxHeight = `${maxHeight}px`;
}

function refreshOpenDropdownPositions() {
    document.querySelectorAll(".dropdown.open").forEach(wrapper => {
        const trigger = wrapper.querySelector(".dropdown-trigger");
        const menu = wrapper.querySelector(".dropdown-menu");
        if (trigger && menu) positionDropdown(wrapper, trigger, menu);
    });
}

window.addEventListener("resize", refreshOpenDropdownPositions);
window.addEventListener("scroll", refreshOpenDropdownPositions, true);

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

async function loadLocale(locale) {
    if (!locale) return;
    try {
        const res = await fetch(`https://rewrite.local/locales/${locale}.json`);
        if (!res.ok) throw new Error('failed to load locale');
        i18n = await res.json();
        applyTranslations();
        const localeSelect = document.getElementById('select-locale');
        if (localeSelect) localeSelect.value = locale;
        document.documentElement.lang = locale;
        syncDropdownUI("locale", locale);
        updateProviderUI();
    } catch (e) {
        console.warn('Locale load failed', e);
    }
}

function applyTranslations() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        if (!key) return;
        const val = i18n[key];
        if (val) el.textContent = val;
    });

    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        if (!key) return;
        const val = i18n[key];
        if (val) el.placeholder = val;
    });

    if (i18n['title.settings']) document.title = i18n['title.settings'];
    syncDropdownUI("provider", document.getElementById("active-provider")?.value || settings.activeProvider || "gemini");
    syncDropdownUI("locale", document.getElementById("select-locale")?.value || settings.locale || "en");
    updateProviderUI();
}
