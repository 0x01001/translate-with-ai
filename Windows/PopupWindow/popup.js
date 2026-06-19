// State
let selectedText = "";
let currentTab = "rewrite"; // "rewrite" | "write" | "translate" | "history"
let lastActiveTextTab = "rewrite";
let aiResult = "";
let historyItems = [];
let lastRequestMeta = null;

// i18n dictionary populated from host-provided locale JSON
let i18n = {};

// Settings default structure (will load from localStorage)
const DEFAULT_ROUTER_BASE_URL = "http://localhost:20128/v1";
const DEFAULT_ROUTER_MODELS = [
    "cx-5.5-combo",
    "openai/gpt-4o-mini",
    "openai/gpt-4o",
    "anthropic/claude-3.5-sonnet",
    "anthropic/claude-3.5-haiku",
    "google/gemini-flash-1.5"
];
const SUPPORTED_PROVIDERS = ["gemini", "openai", "router"];
const DEFAULT_QUICK_TRANSLATE_TARGET_LANG = "Tiếng Việt";

let settings = {
    geminiKey: "",
    geminiModel: "gemini-1.5-flash",
    openaiKey: "",
    openaiModel: "gpt-4o-mini",
    routerKey: "",
    routerBaseUrl: DEFAULT_ROUTER_BASE_URL,
    routerModel: DEFAULT_ROUTER_MODELS[0],
    routerModels: [...DEFAULT_ROUTER_MODELS],
    activeProvider: "router",
    quickTranslateAutoPaste: true,
    quickTranslateTargetLang: DEFAULT_QUICK_TRANSLATE_TARGET_LANG
};

// DOM Elements
const tabs = document.querySelectorAll(".tab-btn");
const tabPanes = document.querySelectorAll(".tab-pane");
const sourcePreview = document.getElementById("source-preview");
const translateSourcePreview = document.getElementById("translate-source-preview");
const sourceBubbleWrapper = document.getElementById("source-bubble-wrapper");
const resultContainer = document.getElementById("result-container");
const resultContentPanel = document.getElementById("result-content-panel");
const triggerSection = document.getElementById("trigger-section");
const aiOutput = document.getElementById("ai-output");
const diffOutput = document.getElementById("diff-output");
const btnShowResult = document.getElementById("btn-show-result");
const btnShowDiff = document.getElementById("btn-show-diff");
const normalOutputWrapper = document.getElementById("normal-output-wrapper");
const diffOutputWrapper = document.getElementById("diff-output-wrapper");
const resultMeta = document.getElementById("result-meta");
const btnCopyResult = document.getElementById("btn-copy-result");
const historyList = document.getElementById("history-list");
const historyEmpty = document.getElementById("history-empty");
const btnClearHistory = document.getElementById("btn-clear-history");

let resizeAnimationFrame = null;
let popupResizeObserver = null;
const promptTemplates = window.ReWritePromptTemplates || {};
const pendingAiProxyRequests = new Map();
let aiProxyRequestSeq = 0;

window.addEventListener("DOMContentLoaded", () => {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "ui_ready" });
    }
    watchPopupResize();
    requestPopupResize();
    // Ask host to send locale if it hasn't already
    if (window.chrome && window.chrome.webview) {
        try { window.chrome.webview.postMessage({ action: "request_locale" }); } catch { }
    }
});

function escapeHtml(text) {
    return text
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

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
        settings.activeProvider = "router";
    }

    settings.quickTranslateAutoPaste = settings.quickTranslateAutoPaste !== false;
    settings.quickTranslateTargetLang = String(settings.quickTranslateTargetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG).trim() || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;
}

function getCurrentModel() {
    if (settings.activeProvider === "gemini") return settings.geminiModel;
    if (settings.activeProvider === "router") return settings.routerModel;
    return settings.openaiModel;
}

function getModelLabel() {
    const model = getCurrentModel() || "AI";

    return model
        .replace(/[\/-]/g, " ")
        .split(" ")
        .filter(Boolean)
        .map(word => word.charAt(0).toUpperCase() + word.slice(1))
        .join(" ");
}

function updateModelLabel() {
    const el = document.getElementById("model-label");
    if (!el) return;
    el.textContent = getModelLabel();
}

function formatTime(date) {
    const locale = i18n['_locale_code'] || 'vi-VN';
    return date.toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit" });
}

function getTabLabel(tab, targetLang) {
    if (tab === "rewrite") return i18n['tab.rewrite'] || "Viết lại";
    if (tab === "write") return i18n['tab.write'] || "Soạn thảo";
    if (tab === "translate") return targetLang ? (i18n['tab.translate_to'] ? `${i18n['tab.translate_to']} ${targetLang}` : `Dịch sang ${targetLang}`) : (i18n['tab.translate'] || "Dịch thuật");
    return i18n['tab.result'] || "Kết quả";
}

function updateResultMeta(isLoading = false) {
    if (!resultMeta) return;
    const label = lastRequestMeta ? getTabLabel(lastRequestMeta.tab, lastRequestMeta.targetLang) : "Kết quả";
    if (isLoading) {
        const gen = i18n['meta.generating'] || 'Đang tạo...';
        resultMeta.textContent = `${label} • ${gen}`;
        return;
    }
    resultMeta.textContent = `${label} • ${formatTime(new Date())}`;
}

async function copyText(text) {
    if (!text) return;
    if (navigator.clipboard && navigator.clipboard.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return;
        } catch (e) {
            console.warn("Clipboard API failed, falling back", e);
        }
    }

    const temp = document.createElement("textarea");
    temp.value = text;
    temp.setAttribute("readonly", "");
    temp.style.position = "absolute";
    temp.style.left = "-9999px";
    document.body.appendChild(temp);
    temp.select();
    document.execCommand("copy");
    document.body.removeChild(temp);
}

function addHistoryItem() {
    if (!aiResult.trim() || !lastRequestMeta) return;
    const item = {
        id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
        text: aiResult.trim(),
        tab: lastRequestMeta.tab,
        targetLang: lastRequestMeta.targetLang || "",
        time: new Date()
    };
    historyItems.unshift(item);
    renderHistory();
}

function renderHistory() {
    if (!historyList || !historyEmpty) return;
    historyList.innerHTML = "";

    if (!historyItems.length) {
        historyEmpty.classList.remove("hidden");
        historyList.appendChild(historyEmpty);
        return;
    }

    historyEmpty.classList.add("hidden");
    historyItems.forEach(item => {
        const entry = document.createElement("div");
        entry.className = "p-2.5 bg-white/3 border border-white/5 rounded-lg flex flex-col gap-1.5 transition-all text-[11px] hover:border-white/10";
        const label = getTabLabel(item.tab, item.targetLang);
        const time = formatTime(item.time);
        const safeText = escapeHtml(item.text).replace(/\n/g, "<br>");
        entry.innerHTML = `
            <div class="flex justify-between items-center text-[10px] text-gray-400">
                <span class="px-1.5 py-0.5 rounded bg-indigo-500/10 border border-indigo-500/10 text-indigo-400 font-semibold">${label}</span>
                <span>${time}</span>
            </div>
            <div class="text-gray-200 wrap-break-word leading-relaxed select-text">${safeText}</div>
            <div class="flex justify-end gap-2 border-t border-white/5 pt-1.5 mt-1">
                <button class="bg-white/5 border border-white/5 hover:bg-white/8 text-[9px] px-2 py-0.5 rounded-md cursor-pointer font-semibold transition-all" data-action="copy" data-id="${item.id}">${i18n['popup.copy'] || 'Copy'}</button>
            </div>
        `;
        historyList.appendChild(entry);
    });
}

function measurePopupHeight() {
    const appContainer = document.querySelector(".app-container");
    if (!appContainer) {
        return Math.ceil(document.documentElement.scrollHeight || document.body.scrollHeight || 0);
    }
    return Math.ceil(appContainer.getBoundingClientRect().height);
}

function requestPopupResize() {
    if (!(window.chrome && window.chrome.webview)) return;

    if (resizeAnimationFrame) {
        cancelAnimationFrame(resizeAnimationFrame);
    }

    resizeAnimationFrame = requestAnimationFrame(() => {
        resizeAnimationFrame = null;
        window.chrome.webview.postMessage({
            action: "resize_popup",
            height: measurePopupHeight()
        });
    });
}

function watchPopupResize() {
    const appContainer = document.querySelector(".app-container");
    if (!appContainer || typeof ResizeObserver === "undefined") return;

    if (popupResizeObserver) {
        popupResizeObserver.disconnect();
    }

    popupResizeObserver = new ResizeObserver(() => {
        requestPopupResize();
    });
    popupResizeObserver.observe(appContainer);
}

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
    syncQuickTranslateTargetSelect();
    updateModelLabel();
}

function getQuickTranslateTargetLang() {
    return settings.quickTranslateTargetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;
}

function syncQuickTranslateTargetSelect(targetLang = getQuickTranslateTargetLang()) {
    const select = document.getElementById("param-lang");
    if (!select) return;

    const value = String(targetLang || DEFAULT_QUICK_TRANSLATE_TARGET_LANG).trim() || DEFAULT_QUICK_TRANSLATE_TARGET_LANG;
    const hasOption = Array.from(select.options).some(option => option.value === value);
    if (!hasOption) {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = value;
        select.appendChild(option);
    }
    select.value = value;
}

function renderPrompt(template, values) {
    return template.replace(/\{\{(\w+)}}/g, (_, key) => values[key] ?? "");
}

function normalizePromptSpacing(prompt) {
    return prompt
        .replace(/[ \t]+\n/g, "\n")
        .replace(/\n{3,}/g, "\n\n")
        .trim();
}

function buildTranslatePrompt(text, targetLang) {
    return normalizePromptSpacing(renderPrompt(promptTemplates.translate, {
        targetLang: targetLang || getQuickTranslateTargetLang(),
        selectedText: text
    }));
}

async function runProviderStream(prompt) {
    aiResult = "";
    if (settings.activeProvider === "gemini") {
        await streamGemini(prompt);
    } else if (settings.activeProvider === "router") {
        await streamRouter(prompt);
    } else {
        await streamOpenAI(prompt);
    }
    return aiResult.trim();
}

function postToHost(message) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
    }
}

// Tab Switching
async function switchTab(tabId) {
    currentTab = tabId;

    // Active tabs headers
    tabs.forEach(btn => {
        if (btn.getAttribute("data-tab") === tabId) {
            btn.classList.add("active", "text-white", "bg-white/8");
            btn.classList.remove("text-gray-400");
        } else {
            btn.classList.remove("active", "text-white", "bg-white/8");
            btn.classList.add("text-gray-400");
        }
    });

    // Active tab content panes
    tabPanes.forEach(pane => {
        if (pane.id === `tab-${tabId}`) {
            pane.classList.remove("hidden");
            pane.classList.add("block");
        } else {
            pane.classList.add("hidden");
            pane.classList.remove("block");
        }
    });

    // Hide/Show source bubble based on tab
    if (tabId === "rewrite") {
        sourceBubbleWrapper.style.display = "block";
        lastActiveTextTab = "rewrite";
    } else if (tabId === "write") {
        sourceBubbleWrapper.style.display = "none";
        lastActiveTextTab = "write";
    } else if (tabId === "translate") {
        lastActiveTextTab = "translate";
    }

    // Hide result container if viewing history
    if (tabId === "history") {
        resultContainer.style.display = "none";
        triggerSection.style.display = "none";
    } else {
        if (aiResult) {
            resultContainer.style.display = "flex";
            triggerSection.style.display = "none";
        } else {
            resultContainer.style.display = "none";
            triggerSection.style.display = "block";
        }
    }
    // aiOutput.innerHTML = await getLoaderHTML();

    // resultContainer.style.display = "block";
    // triggerSection.style.display = "none";

    requestPopupResize();
}

// Register Tabs Listeners
tabs.forEach(btn => {
    btn.addEventListener("click", () => {
        switchTab(btn.getAttribute("data-tab"));
    });
});

// Setup WebView2 Bridge listener
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", event => {
        const data = event.data;

        if (data.event === "set_locale") {
            loadLocale(data.locale);
            return;
        }

        if (data.event === "quick_translate") {
            handleQuickTranslate(data);
            return;
        }

        if (data.event === "ai_proxy_started") {
            handleNativeAiProxyStarted(data);
            return;
        }

        if (data.event === "ai_proxy_chunk") {
            handleNativeAiProxyChunk(data);
            return;
        }

        if (data.event === "ai_proxy_done") {
            handleNativeAiProxyDone(data);
            return;
        }

        if (data.event === "ai_proxy_error") {
            handleNativeAiProxyError(data);
            return;
        }

        if (data.event === "show") {
            selectedText = data.text ? data.text.trim() : "";

            // Clean up previous AI results on fresh trigger
            aiResult = "";
            aiOutput.innerHTML = i18n['popup.waiting'] || "Đang chờ yêu cầu...";
            diffOutput.innerHTML = "";
            resultContainer.style.display = "none";
            btnShowDiff.classList.add("hidden");
            switchResultTab("normal");
            updateResultMeta(true);

            // Set text preview
            if (selectedText) {
                sourcePreview.textContent = selectedText;
                sourcePreview.classList.remove("text-gray-500");
                translateSourcePreview.textContent = selectedText;
                translateSourcePreview.classList.remove("text-gray-500");
                if (data.targetLang) syncQuickTranslateTargetSelect(data.targetLang);
                switchTab(data.mode === "translate" ? "translate" : "rewrite");
                if (data.autoStart) {
                    setTimeout(() => startAIProcess(), 80);
                }
            } else {
                const noSelection = i18n['popup.no_selection'] || "Chưa chọn văn bản nào... Hãy bôi đen văn bản ngoài màn hình và nhấn phím tắt.";
                sourcePreview.textContent = noSelection;
                sourcePreview.classList.add("text-gray-500");
                translateSourcePreview.textContent = noSelection;
                translateSourcePreview.classList.add("text-gray-500");
                switchTab("write");
            }

            const activePromptInput = document.getElementById(`${currentTab}-prompt-input`);
            if (activePromptInput) {
                activePromptInput.focus();
            }
            requestPopupResize();
        }
    });
}

async function handleQuickTranslate(data) {
    refreshAppSettings();
    selectedText = data.text ? data.text.trim() : "";
    const targetLang = data.targetLang || getQuickTranslateTargetLang();
    syncQuickTranslateTargetSelect(targetLang);

    aiResult = "";
    diffOutput.innerHTML = "";
    btnShowDiff.classList.add("hidden");
    switchResultTab("normal");

    if (!selectedText) {
        postToHost({
            action: "quick_translate_error",
            message: i18n['quick_translate.error.no_selection'] || i18n['popup.error.no_text_for_translate'] || "No selected text found."
        });
        return;
    }

    sourcePreview.textContent = selectedText;
    sourcePreview.classList.remove("text-gray-500");
    translateSourcePreview.textContent = selectedText;
    translateSourcePreview.classList.remove("text-gray-500");

    if (settings.quickTranslateAutoPaste === false) {
        postToHost({ action: "quick_translate_show_popup" });
        await switchTab("translate");
        resultContainer.style.display = "none";
        triggerSection.style.display = "block";
        requestPopupResize();
        setTimeout(() => startAIProcess(), 120);
        return;
    }

    try {
        lastRequestMeta = {
            tab: "translate",
            tone: "",
            format: "",
            length: "",
            prompt: "",
            targetLang,
            sourceText: selectedText
        };
        const prompt = buildTranslatePrompt(selectedText, targetLang);
        const translatedText = await runProviderStream(prompt);
        if (!translatedText) {
            throw new Error(i18n['quick_translate.error.empty_result'] || "The translation result was empty.");
        }
        addHistoryItem();
        postToHost({ action: "quick_translate_result", text: translatedText });
    } catch (error) {
        postToHost({
            action: "quick_translate_error",
            message: getFriendlyProviderErrorMessage(error)
        });
    }
}

// Allow dragging the window from any non-interactive empty space
document.addEventListener("mousedown", event => {
    const target = event.target;
    if (!target || target.closest("button, input, select, textarea, a, svg, path, #source-preview, #translate-source-preview, #ai-output, #diff-output, #history-list")) return;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "start_drag" });
    }
});

// Trigger Generation
document.getElementById("btn-generate").addEventListener("click", startAIProcess);
document.getElementById("btn-retry").addEventListener("click", startAIProcess);

let cachedLoaderHTML = null;

async function getLoaderHTML() {
    if (cachedLoaderHTML) {
        return cachedLoaderHTML;
    }

    const response = await fetch("ai-loader.html");

    cachedLoaderHTML = await response.text();

    return cachedLoaderHTML;
}

async function startAIProcess() {
    refreshAppSettings();
    resultContainer.style.display = "flex";
    triggerSection.style.display = "none";
    requestPopupResize();

    const resultPanel = document.querySelector("#ai-output").parentElement.parentElement;
    resultPanel.classList.add("relative", "result-panel", "loading", "ai-output-loading");

    // Cancel overflow and setup loader
    aiOutput.classList.remove("overflow-y-auto");
    aiOutput.classList.add("overflow-hidden");
    aiOutput.innerHTML = await getLoaderHTML();

    toggleButtonsDisabled(true);

    try {
        // Build Prompt
        let prompt = "";
        const activeTab = currentTab === "history" ? lastActiveTextTab : currentTab;

        const tone = document.getElementById(`${activeTab}-param-tone`)?.value || "";
        const format = document.getElementById(`${activeTab}-param-format`)?.value || "";
        const length = document.getElementById(`${activeTab}-param-length`)?.value || "";
        const customPrompt = document.getElementById(`${activeTab}-prompt-input`)?.value.trim() || "";
        const targetLang = document.getElementById("param-lang")?.value || getQuickTranslateTargetLang();

        lastRequestMeta = {
            tab: activeTab,
            tone,
            format,
            length,
            prompt: customPrompt,
            targetLang,
            sourceText: selectedText
        };
        updateResultMeta(true);

        if (activeTab === "rewrite") {
            if (!selectedText) {
                const msg = i18n['popup.error.no_selection'] || 'Lỗi: Không tìm thấy văn bản đã chọn. Vui lòng đóng cửa sổ này, bôi đen văn bản cần viết lại bên ngoài, rồi nhấn phím tắt.';
                aiOutput.innerHTML = `<span class='text-rose-400'>${msg}</span>`;
                return;
            }

            prompt = normalizePromptSpacing(renderPrompt(promptTemplates.rewrite, {
                tone: getToneText(tone),
                format: getFormatText(format),
                length: getLengthText(length),
                customPromptLine: customPrompt ? `- Additional special request: "${customPrompt}"` : "",
                selectedText
            }));
        }
        else if (activeTab === "write") {
            if (!customPrompt) {
                const msg = i18n['popup.error.prompt_required'] || 'Lỗi: Vui lòng nhập nội dung gợi ý (Prompt) hoặc chủ đề bạn muốn soạn thảo.';
                aiOutput.innerHTML = `<span class='text-rose-400'>${msg}</span>`;
                return;
            }

            prompt = normalizePromptSpacing(renderPrompt(promptTemplates.write, {
                customPrompt,
                tone: getToneText(tone),
                format: getFormatText(format),
                length: getLengthText(length),
                contextLine: selectedText ? `- Additional context text: "${selectedText}"` : ""
            }));
        }
        else if (activeTab === "translate") {
            if (!selectedText) {
                const msg = i18n['popup.error.no_text_for_translate'] || 'Lỗi: Không tìm thấy văn bản cần dịch. Vui lòng đóng cửa sổ, bôi đen văn bản cần dịch bên ngoài, rồi nhấn phím tắt.';
                aiOutput.innerHTML = `<span class='text-rose-400'>${msg}</span>`;
                return;
            }

            prompt = buildTranslatePrompt(selectedText, targetLang);
        }
        console.log("Generated Prompt:", prompt);
        await runProviderStream(prompt);

        if (selectedText && (activeTab === "rewrite" || activeTab === "translate")) {
            btnShowDiff.classList.remove("hidden");
            calculateAndRenderDiff();
        } else {
            btnShowDiff.classList.add("hidden");
        }

        updateResultMeta(false);
        addHistoryItem();

    } catch (error) {
        const tpl = i18n['popup.error.api_connection'] || 'Lỗi kết nối API: {0}<br>Vui lòng mở Cài đặt (từ khay hệ thống) để cấu hình API Key chính xác.';
        const msg = tpl.replace('{0}', escapeHtml(getFriendlyProviderErrorMessage(error)));
        aiOutput.innerHTML = `<span class='text-rose-400'>${msg}</span>`;
    } finally {
        // Allow overflow and remove loading
        aiOutput.classList.remove("overflow-hidden");
        aiOutput.classList.add("overflow-y-auto");
        resultContentPanel.classList.remove("justify-center");
        resultPanel.classList.remove("loading", "ai-output-loading");
        toggleButtonsDisabled(false);
    }
}

async function loadLocale(locale) {
    if (!locale) return;
    try {
        const res = await fetch(`https://rewrite.local/locales/${locale}.json`);
        if (!res.ok) throw new Error('failed to load locale');
        i18n = await res.json();
        i18n['_locale_code'] = locale === 'vi' ? 'vi-VN' : (locale === 'en' ? 'en-US' : locale);
        applyTranslations();
        document.querySelectorAll("select").forEach(select => { if (typeof syncCustomSelect === "function") syncCustomSelect(select); });
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

    // set placeholders for inputs that request it
    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        if (!key) return;
        const val = i18n[key];
        if (val) el.placeholder = val;
    });

    if (!selectedText) {
        const src = document.getElementById('source-preview');
        if (src) src.textContent = i18n['popup.no_selection'] || src.textContent;
    }
    if (aiOutput) aiOutput.innerHTML = i18n['popup.waiting'] || aiOutput.innerHTML;
    syncQuickTranslateTargetSelect();
    updateModelLabel();
    document.querySelectorAll("select").forEach(select => {
        if (typeof syncCustomSelect === "function") syncCustomSelect(select);
    });
    requestPopupResize();
}

function getFriendlyProviderErrorMessage(error) {
    const rawMessage = error && error.message ? String(error.message) : String(error || "");
    const message = rawMessage.trim() || (i18n['quick_translate.error.failed'] || "Translation failed.");

    if (/^(load failed|failed to fetch|networkerror)$/i.test(message) || /load failed|failed to fetch|networkerror/i.test(message)) {
        return i18n['popup.error.load_failed_detail'] || "Network request failed before the AI server could respond. If you use 9router/local router, the old WebView request was likely blocked by CORS preflight. ReWrite now tries the native proxy first; please check that the local router is running, the Base URL is correct, and the API key is valid.";
    }

    if (/native_proxy_unavailable/i.test(message)) {
        return i18n['popup.error.load_failed_detail'] || "Native AI proxy is unavailable in this build. Please rebuild/reopen ReWrite, or check your provider settings.";
    }

    return message;
}

function isLocalRouterBaseUrl(baseUrl) {
    try {
        const url = new URL(baseUrl);
        const host = url.hostname.toLowerCase();
        return host === "localhost" || host === "127.0.0.1" || host === "0.0.0.0" || host === "::1";
    } catch {
        return false;
    }
}

function getRouterExtraHeaders(baseUrl) {
    // Local routers such as 9router often fail browser CORS preflight when extra
    // non-simple headers are present. Native proxy does not need this, and the
    // browser fallback should stay as simple as possible.
    if (isLocalRouterBaseUrl(baseUrl)) return {};
    return {
        "HTTP-Referer": "https://rewrite.local",
        "X-Title": "ReWrite"
    };
}

function canUseNativeAiProxy() {
    return !!(window.chrome && window.chrome.webview);
}

function handleNativeAiProxyStarted(data) {
    const pending = pendingAiProxyRequests.get(data.requestId);
    if (!pending) return;
    pending.started = true;
    clearTimeout(pending.unsupportedTimer);
}

function handleNativeAiProxyChunk(data) {
    const pending = pendingAiProxyRequests.get(data.requestId);
    if (!pending) return;
    const text = data.text || "";
    if (!text) return;
    aiResult += text;
    renderStreamingText(aiResult);
}

function handleNativeAiProxyDone(data) {
    const pending = pendingAiProxyRequests.get(data.requestId);
    if (!pending) return;
    pendingAiProxyRequests.delete(data.requestId);
    clearTimeout(pending.unsupportedTimer);
    renderStreamingText(aiResult, true);
    pending.resolve();
}

function handleNativeAiProxyError(data) {
    const pending = pendingAiProxyRequests.get(data.requestId);
    if (!pending) return;
    pendingAiProxyRequests.delete(data.requestId);
    clearTimeout(pending.unsupportedTimer);
    pending.reject(new Error(data.message || (i18n['quick_translate.error.failed'] || "Translation failed.")));
}

async function streamOpenAICompatibleViaNative(prompt, { apiKey, model, baseUrl, extraHeaders = {} }) {
    if (!canUseNativeAiProxy()) {
        throw new Error("native_proxy_unavailable");
    }

    const requestId = `ai-${Date.now()}-${++aiProxyRequestSeq}`;

    return new Promise((resolve, reject) => {
        const unsupportedTimer = setTimeout(() => {
            const pending = pendingAiProxyRequests.get(requestId);
            if (!pending || pending.started) return;
            pendingAiProxyRequests.delete(requestId);
            reject(new Error("native_proxy_unavailable"));
        }, 900);

        pendingAiProxyRequests.set(requestId, {
            started: false,
            unsupportedTimer,
            resolve,
            reject
        });

        postToHost({
            action: "ai_proxy_request",
            requestId,
            provider: "openai-compatible",
            apiKey,
            model,
            baseUrl,
            prompt,
            extraHeaders
        });
    });
}

async function streamGeminiViaNative(prompt, { apiKey, model }) {
    if (!canUseNativeAiProxy()) {
        throw new Error("native_proxy_unavailable");
    }

    const requestId = `ai-${Date.now()}-${++aiProxyRequestSeq}`;

    return new Promise((resolve, reject) => {
        const unsupportedTimer = setTimeout(() => {
            const pending = pendingAiProxyRequests.get(requestId);
            if (!pending || pending.started) return;
            pendingAiProxyRequests.delete(requestId);
            reject(new Error("native_proxy_unavailable"));
        }, 900);

        pendingAiProxyRequests.set(requestId, {
            started: false,
            unsupportedTimer,
            resolve,
            reject
        });

        postToHost({
            action: "ai_proxy_request",
            requestId,
            provider: "gemini",
            apiKey,
            model,
            prompt,
            extraHeaders: {}
        });
    });
}

// update document title if provided
if (typeof applyTranslations === 'function') {
    const _oldApply = applyTranslations;
    applyTranslations = function() {
        _oldApply();
        if (i18n['title.popup']) document.title = i18n['title.popup'];
    }
}

// Gemini API Streaming Implementation
async function streamGemini(prompt) {
    if (!settings.geminiKey) {
        throw new Error(i18n['popup.error.gemini_key_missing'] || "Chưa cấu hình Gemini API Key. Hãy cấu hình thông qua cửa sổ Cài đặt của ứng dụng.");
    }

    try {
        await streamGeminiViaNative(prompt, { apiKey: settings.geminiKey, model: settings.geminiModel });
        return;
    } catch (error) {
        if (!/native_proxy_unavailable/i.test(error && error.message ? error.message : String(error))) {
            throw error;
        }
        console.warn("Native Gemini proxy unavailable, falling back to WebView fetch", error);
    }

    const url = `https://generativelanguage.googleapis.com/v1beta/models/${settings.geminiModel}:streamGenerateContent?key=${settings.geminiKey}`;

    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            system_instruction: {
                parts: [{ text: "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate." }]
            },
            contents: [{ parts: [{ text: prompt }] }],
            generationConfig: { temperature: 0.3 }
        })
    });

    if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const errMsg = errorData.error?.message || `HTTP ${response.status}`;
        throw new Error(errMsg);
    }

    aiOutput.innerHTML = "";
    const reader = response.body.getReader();
    const decoder = new TextDecoder("utf-8");
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });

        let boundaryIndex;
        while ((boundaryIndex = findJsonEnd(buffer)) !== -1) {
            let jsonStr = buffer.substring(0, boundaryIndex).trim();
            buffer = buffer.substring(boundaryIndex);

            if (jsonStr.startsWith(",")) jsonStr = jsonStr.substring(1).trim();
            if (jsonStr.startsWith("[")) jsonStr = jsonStr.substring(1).trim();
            if (jsonStr.endsWith("]")) jsonStr = jsonStr.substring(0, jsonStr.length - 1).trim();

            if (jsonStr) {
                try {
                    const parsed = JSON.parse(jsonStr);
                    const chunkText = parsed.candidates?.[0]?.content?.parts?.[0]?.text || "";
                    if (chunkText) {
                        aiResult += chunkText;
                        renderStreamingText(aiResult);
                    }
                } catch (e) {
                    buffer = jsonStr + buffer;
                    break;
                }
            }
        }
    }

    if (buffer.trim()) {
        try {
            let cleanStr = buffer.trim();
            if (cleanStr.startsWith(",")) cleanStr = cleanStr.substring(1).trim();
            if (cleanStr.endsWith("]")) cleanStr = cleanStr.substring(0, cleanStr.length - 1).trim();
            const parsed = JSON.parse(cleanStr);
            const chunkText = parsed.candidates?.[0]?.content?.parts?.[0]?.text || "";
            if (chunkText) {
                aiResult += chunkText;
            }
        } catch (e) { }
    }

    renderStreamingText(aiResult, true);
}

function findJsonEnd(str) {
    let depth = 0;
    let inString = false;
    let escape = false;

    for (let i = 0; i < str.length; i++) {
        const char = str[i];
        if (escape) {
            escape = false;
            continue;
        }
        if (char === "\\") {
            escape = true;
            continue;
        }
        if (char === '"') {
            inString = !inString;
            continue;
        }
        if (!inString) {
            if (char === "{") depth++;
            else if (char === "}") {
                depth--;
                if (depth === 0) return i + 1;
            }
        }
    }
    return -1;
}

// OpenAI-compatible API Streaming Implementation
async function streamOpenAI(prompt) {
    if (!settings.openaiKey) {
        throw new Error(i18n['popup.error.openai_key_missing'] || "Chưa cấu hình OpenAI API Key. Hãy cấu hình thông qua cửa sổ Cài đặt của ứng dụng.");
    }

    await streamOpenAICompatible(prompt, {
        apiKey: settings.openaiKey,
        model: settings.openaiModel,
        baseUrl: "https://api.openai.com/v1"
    });
}

async function streamRouter(prompt) {
    if (!settings.routerKey) {
        throw new Error(i18n['popup.error.router_key_missing'] || "Chưa cấu hình OpenRouter / 9router API Key. Hãy cấu hình trong Cài đặt.");
    }

    const baseUrl = settings.routerBaseUrl || DEFAULT_ROUTER_BASE_URL;
    await streamOpenAICompatible(prompt, {
        apiKey: settings.routerKey,
        model: settings.routerModel,
        baseUrl,
        extraHeaders: getRouterExtraHeaders(baseUrl)
    });
}

function buildChatCompletionsUrl(baseUrl) {
    const cleanBaseUrl = String(baseUrl || "").trim().replace(/\/+$/, "");
    if (!cleanBaseUrl) return "https://api.openai.com/v1/chat/completions";
    if (cleanBaseUrl.endsWith("/chat/completions")) return cleanBaseUrl;
    return `${cleanBaseUrl}/chat/completions`;
}

async function streamOpenAICompatible(prompt, { apiKey, model, baseUrl, extraHeaders = {} }) {
    try {
        await streamOpenAICompatibleViaNative(prompt, { apiKey, model, baseUrl, extraHeaders });
        return;
    } catch (error) {
        if (!/native_proxy_unavailable/i.test(error && error.message ? error.message : String(error))) {
            throw error;
        }
        console.warn("Native AI proxy unavailable, falling back to WebView fetch", error);
    }

    const response = await fetch(buildChatCompletionsUrl(baseUrl), {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${apiKey}`,
            ...extraHeaders
        },
        body: JSON.stringify({
            model,
            messages: [
                { role: "system", content: "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate." },
                { role: "user", content: prompt }
            ],
            stream: true,
            temperature: 0.3
        })
    });

    if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const errMsg = errorData.error?.message || `HTTP ${response.status}`;
        throw new Error(errMsg);
    }

    aiOutput.innerHTML = "";
    const reader = response.body.getReader();
    const decoder = new TextDecoder("utf-8");
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop();

        for (const line of lines) {
            const cleanLine = line.trim();
            if (!cleanLine || cleanLine === "data: [DONE]") continue;

            if (cleanLine.startsWith("data: ")) {
                try {
                    const parsed = JSON.parse(cleanLine.substring(6));
                    const chunkText = parsed.choices?.[0]?.delta?.content || "";
                    if (chunkText) {
                        aiResult += chunkText;
                        renderStreamingText(aiResult);
                    }
                } catch (e) {
                    console.error("Error parsing OpenAI-compatible stream chunk", e);
                }
            }
        }
    }

    renderStreamingText(aiResult, true);
}

function renderStreamingText(text, isFinished = false) {
    let formatted = escapeHtml(text);
    formatted = formatted.replace(/\n/g, "<br>");

    if (isFinished) {
        aiOutput.innerHTML = formatted;
    } else {
        aiOutput.innerHTML = formatted + '<span class="stream-cursor"></span>';
    }
    aiOutput.scrollTop = aiOutput.scrollHeight;
}

function getToneText(tone) {
    const tones = {
        professional: "Professional, formal, polite, scientific",
        casual: "Friendly, conversational, everyday, natural",
        academic: "Academic, formal, using research vocabulary, structured",
        creative: "Creative, vivid, expressive, imaginative",
        humorous: "Humorous, witty, light-hearted, cheerful",
        direct: "Direct, concise, to the point, straightforward"
    };
    return tones[tone] || tone;
}

function getFormatText(format) {
    const formats = {
        paragraph: "Smooth, flowing paragraph",
        bullets: "Bullet list, clearly presenting key points",
        email: "Professional email with subject line, opening and closing",
        short: "Short, concise message for chat or SMS",
        essay: "Detailed essay with thorough arguments and analysis"
    };
    return formats[format] || format;
}

function getLengthText(length) {
    const lengths = {
        short: "Short, as concise as possible",
        medium: "Medium length, well-balanced with adequate information",
        long: "Long, detailed, expanded with comprehensive information"
    };
    return lengths[length] || length;
}

function toggleButtonsDisabled(disabled) {
    // Disable all native buttons and apply a dimming class for visual feedback
    document.querySelectorAll('button').forEach(btn => {
        try { btn.disabled = disabled; } catch (e) { }
        if (disabled) btn.classList.add('disabled-dim'); else btn.classList.remove('disabled-dim');
    });

    // Also ensure tab buttons in `tabs` follow the same state (redundant but explicit)
    tabs.forEach(btn => {
        try { btn.disabled = disabled; } catch (e) { }
    });

    // Remove focus while generating to avoid accidental keyboard interactions
    if (disabled && document.activeElement) {
        try { document.activeElement.blur(); } catch (e) { }
    }
}

function switchResultTab(tabName) {
    if (tabName === "normal") {
        btnShowResult.classList.add("text-white", "bg-white/8");
        btnShowDiff.classList.remove("text-white", "bg-white/8");
        btnShowResult.classList.remove("text-gray-400");
        btnShowDiff.classList.add("text-gray-400");
        normalOutputWrapper.classList.remove("hidden");
        diffOutputWrapper.classList.add("hidden");
    } else {
        btnShowResult.classList.remove("text-white", "bg-white/8");
        btnShowDiff.classList.add("text-white", "bg-white/8");
        btnShowResult.classList.add("text-gray-400");
        btnShowDiff.classList.remove("text-gray-400");
        normalOutputWrapper.classList.add("hidden");
        diffOutputWrapper.classList.remove("hidden");
    }
}

btnShowResult.addEventListener("click", () => switchResultTab("normal"));
btnShowDiff.addEventListener("click", () => switchResultTab("diff"));

if (btnCopyResult) {
    btnCopyResult.addEventListener("click", () => {
        if (aiResult) copyText(aiResult);
    });
}

if (btnClearHistory) {
    btnClearHistory.addEventListener("click", () => {
        historyItems = [];
        renderHistory();
    });
}

if (historyList) {
    historyList.addEventListener("click", event => {
        const button = event.target.closest("button[data-action]");
        if (!button) return;
        const id = button.getAttribute("data-id");
        const item = historyItems.find(entry => entry.id === id);
        if (item) copyText(item.text);
    });
}

document.getElementById("btn-replace").addEventListener("click", () => {
    if (!aiResult) return;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "paste",
            text: aiResult
        });
    }
});

function calculateAndRenderDiff() {
    if (!selectedText || !aiResult) return;

    const oldWords = selectedText.split(/(\s+)/);
    const newWords = aiResult.split(/(\s+)/);

    const oldFiltered = oldWords.filter(w => w !== "");
    const newFiltered = newWords.filter(w => w !== "");

    const diff = computeLcsDiff(oldFiltered, newFiltered);

    let html = "";
    diff.forEach(token => {
        const textEscaped = token.text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
        if (token.type === "ins") {
            if (textEscaped.trim() === "") {
                html += textEscaped;
            } else {
                html += `<span class="diff-ins">${textEscaped}</span>`;
            }
        } else if (token.type === "del") {
            if (textEscaped.trim() !== "") {
                html += `<span class="diff-del">${textEscaped}</span>`;
            }
        } else {
            if (textEscaped === "\n") {
                html += "<br>";
            } else {
                html += `<span class="diff-normal">${textEscaped}</span>`;
            }
        }
    });

    diffOutput.innerHTML = html;
}

function computeLcsDiff(oldList, newList) {
    const m = oldList.length;
    const n = newList.length;
    const dp = Array(m + 1).fill(0).map(() => Array(n + 1).fill(0));

    for (let i = 1; i <= m; i++) {
        for (let j = 1; j <= n; j++) {
            const wordA = oldList[i - 1].trim().toLowerCase();
            const wordB = newList[j - 1].trim().toLowerCase();
            const isMatch = (wordA === wordB && wordA !== "") || (oldList[i - 1] === newList[j - 1]);

            if (isMatch) {
                dp[i][j] = dp[i - 1][j - 1] + 1;
            } else {
                dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
            }
        }
    }

    let i = m, j = n;
    const result = [];

    while (i > 0 || j > 0) {
        if (i > 0 && j > 0) {
            const wordA = oldList[i - 1].trim().toLowerCase();
            const wordB = newList[j - 1].trim().toLowerCase();
            const isMatch = (wordA === wordB && wordA !== "") || (oldList[i - 1] === newList[j - 1]);

            if (isMatch) {
                result.unshift({ type: "normal", text: newList[j - 1] });
                i--; j--;
                continue;
            }
        }

        if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
            result.unshift({ type: "ins", text: newList[j - 1] });
            j--;
        } else {
            result.unshift({ type: "del", text: oldList[i - 1] });
            i--;
        }
    }
    return result;
}

document.addEventListener("DOMContentLoaded", () => {
    refreshAppSettings();
    renderHistory();

    // Prevent zoom shortcuts
    window.addEventListener('keydown', (e) => {
        if (e.ctrlKey && (e.key === '=' || e.key === '-' || e.key === '0' || e.key === '+' || e.key === '_')) {
            e.preventDefault();
        }
    }, { passive: false });

    window.addEventListener('wheel', (e) => {
        if (e.ctrlKey) e.preventDefault();
    }, { passive: false });

    // Setup Advanced Toggles
    document.querySelectorAll(".advanced-options-wrapper").forEach(wrapper => {
        const toggleBtn = wrapper.querySelector(".advanced-toggle-btn");
        const panel = wrapper.querySelector(".advanced-options-panel");
        if (!toggleBtn || !panel) return;

        toggleBtn.addEventListener("click", () => {
            const isOpen = wrapper.classList.contains("open");
            wrapper.classList.toggle("open");
            if (isOpen) {
                panel.style.maxHeight = "0";
                panel.style.opacity = "0";
                toggleBtn.querySelector(".expand-icon").style.transform = "rotate(0deg)";
            } else {
                panel.style.maxHeight = "300px";
                panel.style.opacity = "1";
                toggleBtn.querySelector(".expand-icon").style.transform = "rotate(180deg)";
            }
            requestPopupResize();
        });
    });
});
