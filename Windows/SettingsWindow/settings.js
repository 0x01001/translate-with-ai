// State
const DEFAULT_ROUTER_BASE_URL = "https://openrouter.ai/api/v1";
const DEFAULT_ROUTER_MODELS = [
    "openai/gpt-4o-mini",
    "openai/gpt-4o",
    "anthropic/claude-3.5-sonnet",
    "anthropic/claude-3.5-haiku",
    "google/gemini-flash-1.5"
];
const SUPPORTED_PROVIDERS = ["gemini", "openai", "router"];
const DEFAULT_QUICK_TRANSLATE_TARGET_LANG = "Tiếng Việt";
const PROVIDER_LABEL_KEYS = {
    gemini: "provider.gemini",
    openai: "provider.openai",
    router: "provider.router"
};

let settings = {
    geminiKey: "",
    geminiModel: "gemini-1.5-flash",
    openaiKey: "",
    openaiModel: "gpt-4o-mini",
    routerKey: "",
    routerBaseUrl: DEFAULT_ROUTER_BASE_URL,
    routerModel: DEFAULT_ROUTER_MODELS[0],
    routerModels: [...DEFAULT_ROUTER_MODELS],
    activeProvider: "gemini",
    quickTranslateAutoPaste: true,
    quickTranslateTargetLang: DEFAULT_QUICK_TRANSLATE_TARGET_LANG,
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
const routerPanel = document.getElementById("router-panel");
const activeProviderTrigger = document.getElementById("active-provider-trigger");
const activeProviderText = document.getElementById("active-provider-text");
const activeProviderMenu = document.getElementById("active-provider-menu");
const routerModelInput = document.getElementById("router-model");
const routerModelTrigger = document.getElementById("router-model-trigger");
const routerModelText = document.getElementById("router-model-text");
const routerModelMenu = document.getElementById("router-model-menu");
const routerModelAddInput = document.getElementById("router-model-add");
const routerModelAddBtn = document.getElementById("router-model-add-btn");
const routerModelList = document.getElementById("router-model-list");
const localeTrigger = document.getElementById("select-locale-trigger");
const localeText = document.getElementById("select-locale-text");
const localeMenu = document.getElementById("select-locale-menu");
const quickTranslateAutoPasteInput = document.getElementById("quick-translate-auto-paste");
const quickTranslateTargetLangInput = document.getElementById("quick-translate-target-lang");

function normalizeRouterModels(models, selectedModel) {
    const source = Array.isArray(models) && models.length > 0 ? models : DEFAULT_ROUTER_MODELS;
    const normalized = [];

    source.forEach(model => {
        const value = String(model || "").trim();
        if (value && !normalized.includes(value)) normalized.push(value);
    });

    const selected = String(selectedModel || "").trim();
    if (selected && !normalized.includes(selected)) normalized.unshift(selected);

    return normalized.length > 0 ? normalized : [DEFAULT_ROUTER_MODELS[0]];
}

function normalizeSettings() {
    settings.routerBaseUrl = String(settings.routerBaseUrl || DEFAULT_ROUTER_BASE_URL).trim() || DEFAULT_ROUTER_BASE_URL;
    settings.routerModel = String(settings.routerModel || DEFAULT_ROUTER_MODELS[0]).trim() || DEFAULT_ROUTER_MODELS[0];
    settings.routerModels = normalizeRouterModels(settings.routerModels, settings.routerModel);

    if (!settings.routerModels.includes(settings.routerModel)) {
        settings.routerModel = settings.routerModels[0];
    }

    if (!SUPPORTED_PROVIDERS.includes(settings.activeProvider)) {
        settings.activeProvider = "gemini";
    }

    settings.quickTranslateAutoPaste = settings.quickTranslateAutoPaste !== false;
    settings.quickTranslateTargetLang = String(settings.quickTranslateTargetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG).trim() || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;
}

document.addEventListener("DOMContentLoaded", () => {
    refreshAppSettings();
    bindDropdowns();
    bindRouterModelControls();
    updateProviderUI();
    
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

    normalizeSettings();

    // Sync UI from settings
    document.getElementById("gemini-key").value = settings.geminiKey || "";
    document.getElementById("gemini-model").value = settings.geminiModel || "";
    document.getElementById("openai-key").value = settings.openaiKey || "";
    document.getElementById("openai-model").value = settings.openaiModel || "";
    document.getElementById("router-key").value = settings.routerKey || "";
    document.getElementById("router-base-url").value = settings.routerBaseUrl || DEFAULT_ROUTER_BASE_URL;
    if (routerModelInput) routerModelInput.value = settings.routerModel || DEFAULT_ROUTER_MODELS[0];
    document.getElementById("active-provider").value = settings.activeProvider || "gemini";
    const localeSelect = document.getElementById('select-locale');
    if (localeSelect) localeSelect.value = settings.locale || 'en';
    if (quickTranslateAutoPasteInput) quickTranslateAutoPasteInput.checked = settings.quickTranslateAutoPaste !== false;
    if (quickTranslateTargetLangInput) quickTranslateTargetLangInput.value = settings.quickTranslateTargetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;

    renderRouterModels();
    syncDropdownUI("provider", document.getElementById("active-provider").value);
    syncDropdownUI("routerModel", routerModelInput ? routerModelInput.value : settings.routerModel);
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
    settings.routerKey = document.getElementById("router-key").value.trim();
    settings.routerBaseUrl = document.getElementById("router-base-url").value.trim() || DEFAULT_ROUTER_BASE_URL;
    settings.routerModel = (routerModelInput && routerModelInput.value.trim()) || settings.routerModel || DEFAULT_ROUTER_MODELS[0];
    settings.routerModels = normalizeRouterModels(settings.routerModels, settings.routerModel);
    settings.activeProvider = document.getElementById("active-provider").value;
    settings.quickTranslateAutoPaste = quickTranslateAutoPasteInput ? quickTranslateAutoPasteInput.checked : true;
    settings.quickTranslateTargetLang = (quickTranslateTargetLangInput && quickTranslateTargetLangInput.value) || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;
    normalizeSettings();

    const localeSelect = document.getElementById('select-locale');
    if (localeSelect) settings.locale = localeSelect.value;
    
    persistSettingsToLocalStorage();
    renderRouterModels();
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

function syncSettingsFromForm() {
    const valueOf = id => document.getElementById(id)?.value?.trim() || "";

    settings.geminiKey = valueOf("gemini-key");
    settings.geminiModel = valueOf("gemini-model") || settings.geminiModel || "gemini-1.5-flash";
    settings.openaiKey = valueOf("openai-key");
    settings.openaiModel = valueOf("openai-model") || settings.openaiModel || "gpt-4o-mini";
    settings.routerKey = valueOf("router-key");
    settings.routerBaseUrl = valueOf("router-base-url") || DEFAULT_ROUTER_BASE_URL;
    settings.routerModel = (routerModelInput && routerModelInput.value.trim()) || settings.routerModel || DEFAULT_ROUTER_MODELS[0];
    settings.activeProvider = valueOf("active-provider") || settings.activeProvider || "gemini";
    settings.quickTranslateAutoPaste = quickTranslateAutoPasteInput ? quickTranslateAutoPasteInput.checked : true;
    settings.quickTranslateTargetLang = (quickTranslateTargetLangInput && quickTranslateTargetLangInput.value) || settings.quickTranslateTargetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;

    const localeSelect = document.getElementById('select-locale');
    if (localeSelect) settings.locale = localeSelect.value;

    normalizeSettings();
}

function persistSettingsToLocalStorage() {
    syncSettingsFromForm();
    localStorage.setItem("rewrite_settings", JSON.stringify(settings));
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
            if (hotkeyInput) hotkeyInput.value = data.hotkey || "Alt+X";
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

function getProviderLabel(provider) {
    const key = PROVIDER_LABEL_KEYS[provider] || PROVIDER_LABEL_KEYS.gemini;
    const fallback = {
        gemini: "Gemini (Google)",
        openai: "OpenAI",
        router: "OpenRouter / 9router"
    };
    return i18n[key] || fallback[provider] || fallback.gemini;
}

function updateProviderUI() {
    const provider = (activeProviderInput && activeProviderInput.value) || settings.activeProvider || "gemini";

    if (geminiPanel) geminiPanel.style.display = provider === "gemini" ? "block" : "none";
    if (openaiPanel) openaiPanel.style.display = provider === "openai" ? "block" : "none";
    if (routerPanel) routerPanel.style.display = provider === "router" ? "block" : "none";

    if (activeProviderLabel) {
        activeProviderLabel.textContent = getProviderLabel(provider);
    }
}

function bindDropdowns() {
    setupDropdown("provider", activeProviderTrigger, activeProviderMenu, value => {
        if (activeProviderInput) activeProviderInput.value = value;
        settings.activeProvider = value;
        updateProviderUI();
    });

    setupDropdown("routerModel", routerModelTrigger, routerModelMenu, value => {
        setRouterModel(value);
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

    menu.addEventListener("click", event => {
        const option = event.target.closest(".dropdown-option");
        if (!option || !menu.contains(option)) return;

        event.preventDefault();
        const value = option.getAttribute("data-value") || "";
        onSelect(value);
        syncDropdownUI(name, value);
        closeAllDropdowns();
    });
}

function getDropdownParts(name) {
    if (name === "provider") {
        return { triggerText: activeProviderText, menu: activeProviderMenu, trigger: activeProviderTrigger };
    }
    if (name === "routerModel") {
        return { triggerText: routerModelText, menu: routerModelMenu, trigger: routerModelTrigger };
    }
    return { triggerText: localeText, menu: localeMenu, trigger: localeTrigger };
}

function syncDropdownUI(name, value) {
    const { triggerText, menu, trigger } = getDropdownParts(name);
    if (!menu || !triggerText || !trigger) return;

    const option = menu.querySelector(`.dropdown-option[data-value="${CSS.escape(value)}"]`);
    if (option) {
        const label = option.getAttribute("data-label-key");
        triggerText.textContent = label && i18n[label] ? i18n[label] : option.textContent.trim();
    } else if (value) {
        triggerText.textContent = value;
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

function bindRouterModelControls() {
    if (routerModelAddBtn) {
        routerModelAddBtn.addEventListener("click", addRouterModelFromInput);
    }

    if (routerModelAddInput) {
        routerModelAddInput.addEventListener("keydown", event => {
            if (event.key === "Enter") {
                event.preventDefault();
                addRouterModelFromInput();
            }
        });
    }

    if (routerModelList) {
        routerModelList.addEventListener("click", event => {
            const button = event.target.closest("button[data-model]");
            if (!button || !routerModelList.contains(button)) return;
            removeRouterModel(button.getAttribute("data-model") || "");
        });
    }
}

function setRouterModel(model) {
    const value = String(model || "").trim();
    if (!value) return;

    if (!settings.routerModels.includes(value)) {
        settings.routerModels.push(value);
    }
    settings.routerModel = value;
    if (routerModelInput) routerModelInput.value = value;
    renderRouterModels();
    persistSettingsToLocalStorage();
}

function addRouterModelFromInput() {
    if (!routerModelAddInput) return;
    const value = routerModelAddInput.value.trim();
    if (!value) return;

    setRouterModel(value);
    routerModelAddInput.value = "";
}

function removeRouterModel(model) {
    const value = String(model || "").trim();
    if (!value) return;

    settings.routerModels = settings.routerModels.filter(item => item !== value);
    if (settings.routerModels.length === 0) {
        settings.routerModels = [DEFAULT_ROUTER_MODELS[0]];
    }

    if (settings.routerModel === value || !settings.routerModels.includes(settings.routerModel)) {
        settings.routerModel = settings.routerModels[0];
    }
    if (routerModelInput) routerModelInput.value = settings.routerModel;

    renderRouterModels();
    persistSettingsToLocalStorage();
}

function renderRouterModels() {
    normalizeSettings();
    if (routerModelInput) routerModelInput.value = settings.routerModel;
    renderRouterModelOptions();
    renderRouterModelList();
    syncDropdownUI("routerModel", settings.routerModel);
}

function renderRouterModelOptions() {
    if (!routerModelMenu) return;
    routerModelMenu.innerHTML = "";

    settings.routerModels.forEach(model => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "dropdown-option";
        option.setAttribute("data-value", model);
        option.setAttribute("aria-selected", model === settings.routerModel ? "true" : "false");

        const label = document.createElement("span");
        label.textContent = model;
        option.appendChild(label);
        routerModelMenu.appendChild(option);
    });
}

function renderRouterModelList() {
    if (!routerModelList) return;
    routerModelList.innerHTML = "";
    const removeLabel = i18n['settings.router_remove_model'] || "Remove model";

    settings.routerModels.forEach(model => {
        const pill = document.createElement("span");
        pill.className = "model-pill";
        if (model === settings.routerModel) pill.classList.add("active");

        const name = document.createElement("span");
        name.className = "model-name";
        name.textContent = model;
        pill.appendChild(name);

        const button = document.createElement("button");
        button.type = "button";
        button.className = "model-remove";
        button.setAttribute("data-model", model);
        button.setAttribute("aria-label", `${removeLabel}: ${model}`);
        button.title = removeLabel;
        button.textContent = "×";
        pill.appendChild(button);

        routerModelList.appendChild(pill);
    });
}

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
    renderRouterModelList();
    syncDropdownUI("provider", document.getElementById("active-provider")?.value || settings.activeProvider || "gemini");
    syncDropdownUI("routerModel", document.getElementById("router-model")?.value || settings.routerModel || DEFAULT_ROUTER_MODELS[0]);
    syncDropdownUI("locale", document.getElementById("select-locale")?.value || settings.locale || "en");
    updateProviderUI();
}
