function postMessage(payload) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(payload);
    }
}

let i18n = {};

function applyTranslations() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        if (!key) return;
        const val = i18n[key];
        if (val) el.textContent = val;
    });
}
// update document.title when translations applied
if (typeof applyTranslations === 'function') {
    const _oldApply = applyTranslations;
    applyTranslations = function() {
        _oldApply();
        if (i18n['title.tutorial']) document.title = i18n['title.tutorial'];
    }
}

async function loadLocale(locale) {
    if (!locale) return;
    try {
        const res = await fetch(`https://rewrite.local/locales/${locale}.json`);
        if (!res.ok) throw new Error('failed to load locale');
        i18n = await res.json();
        applyTranslations();
    } catch (e) {
        console.warn('Locale load failed', e);
    }
}

function resolveVideoUrl() {
    const params = new URLSearchParams(window.location.search);
    const fromQuery = params.get("video") || params.get("embed") || "https://www.youtube.com/embed/bM_BwH0zYKY?si=XE0ZZ85YfHd8pqCk";
    const fromStorage = localStorage.getItem("rewrite_tutorial_video") || "https://www.youtube.com/embed/bM_BwH0zYKY?si=XE0ZZ85YfHd8pqCk";
    return fromQuery || fromStorage;
}

function setupVideo() {
    const iframe = document.getElementById("tutorial-video");
    const placeholder = document.getElementById("video-placeholder");

    if (!iframe || !placeholder) return;

    const url = resolveVideoUrl();
    if (url) {
        iframe.src = url+"&rel=0&iv_load_policy=3&fs=0&disablekb=1";
        iframe.classList.remove("hidden");
        placeholder.classList.add("hidden");
        return;
    }

    iframe.classList.add("hidden");
    placeholder.classList.remove("hidden");
    placeholder.classList.add("flex");
}

function setupActions() {
    const closeButtons = [
        document.getElementById("btn-close"),
        document.getElementById("btn-close-header"),
        document.getElementById("btn-skip")
    ].filter(Boolean);

    closeButtons.forEach(button => {
        button.addEventListener("click", () => {
            postMessage({ action: "close" });
        });
    });
}

window.addEventListener("DOMContentLoaded", () => {
    setupVideo();
    setupActions();
    postMessage({ action: "ui_ready" });
    if (window.chrome && window.chrome.webview) {
        try { window.chrome.webview.postMessage({ action: "request_locale" }); } catch { }
        window.chrome.webview.addEventListener("message", event => {
            const data = event.data;
            if (data.event === 'set_locale') loadLocale(data.locale);
        });
    }
});
