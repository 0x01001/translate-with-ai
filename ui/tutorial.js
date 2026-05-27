function postMessage(payload) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(payload);
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
        iframe.style.display = "block";
        placeholder.style.display = "none";
        return;
    }

    iframe.style.display = "none";
    placeholder.style.display = "flex";
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
});
