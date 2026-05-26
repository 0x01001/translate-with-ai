// State
let selectedText = "";
let currentTab = "rewrite"; // "rewrite" | "write" | "translate" | "settings"
let lastActiveTextTab = "rewrite";
let aiResult = "";
let activeTargetHwnd = null;
let currentHotkeyText = "Ctrl+Shift+A";

// Settings default structure
let settings = {
    geminiKey: "",
    geminiModel: "gemini-1.5-flash",
    openaiKey: "",
    openaiModel: "gpt-4o-mini",
    activeProvider: "gemini",
    autostart: true
};

// DOM Elements
const tabs = document.querySelectorAll(".tab-btn");
const tabPanes = document.querySelectorAll(".tab-pane");
const sourcePreview = document.getElementById("source-preview");
const translateSourcePreview = document.getElementById("translate-source-preview");
const sourceBubbleWrapper = document.getElementById("source-bubble-wrapper");
const promptInput = document.getElementById("prompt-input");
const resultContainer = document.getElementById("result-container");
const triggerSection = document.getElementById("trigger-section");
const aiOutput = document.getElementById("ai-output");
const diffOutput = document.getElementById("diff-output");
const btnShowResult = document.getElementById("btn-show-result");
const btnShowDiff = document.getElementById("btn-show-diff");
const normalOutputWrapper = document.getElementById("normal-output-wrapper");
const diffOutputWrapper = document.getElementById("diff-output-wrapper");
const appHeader = document.querySelector(".app-header");
const hotkeyInput = document.getElementById("hotkey-input");
const hotkeyError = document.getElementById("hotkey-error");

function getHotkeyHint() {
    return currentHotkeyText ? `nhấn ${currentHotkeyText}` : "nhấn phím tắt";
}

function buildEmptyText() {
    return `Chưa chọn văn bản nào... Hãy bôi đen văn bản ngoài màn hình và ${getHotkeyHint()}.`;
}

// Load Settings from LocalStorage
function loadSettings() {
    const saved = localStorage.getItem("rewrite_settings") || localStorage.getItem("ai_portal_settings");
    if (saved) {
        try {
            settings = { ...settings, ...JSON.parse(saved) };
        } catch (e) {
            console.error("Failed to parse settings", e);
        }
    }
    
    // Fill Settings inputs
    document.getElementById("gemini-key").value = settings.geminiKey;
    document.getElementById("gemini-model").value = settings.geminiModel;
    document.getElementById("openai-key").value = settings.openaiKey;
    document.getElementById("openai-model").value = settings.openaiModel;
    document.getElementById("active-provider").value = settings.activeProvider;
    
    // Ask C# for startup status
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "get_startup" });
        window.chrome.webview.postMessage({ action: "get_hotkey" });
    }
}

// Save Settings to LocalStorage
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
    toast.classList.add("show");
    setTimeout(() => {
        toast.classList.remove("show");
    }, 2000);
}

// Tab Switching
function switchTab(tabId) {
    currentTab = tabId;
    
    // Active tabs headers
    tabs.forEach(btn => {
        if (btn.getAttribute("data-tab") === tabId) {
            btn.classList.add("active");
        } else {
            btn.classList.remove("active");
        }
    });

    // Active tab content panes
    tabPanes.forEach(pane => {
        if (pane.id === `tab-${tabId}` || (tabId === "write" && pane.id === "tab-write-rewrite") || (tabId === "rewrite" && pane.id === "tab-write-rewrite")) {
            pane.classList.add("active");
        } else {
            pane.classList.remove("active");
        }
    });

    // Hide/Show source bubble based on tab
    if (tabId === "write") {
        sourceBubbleWrapper.style.display = "none";
        lastActiveTextTab = "write";
    } else if (tabId === "rewrite") {
        sourceBubbleWrapper.style.display = "block";
        lastActiveTextTab = "rewrite";
    } else if (tabId === "translate") {
        lastActiveTextTab = "translate";
    }

    // Hide result container if viewing settings
    if (tabId === "settings") {
        resultContainer.style.display = "none";
        triggerSection.style.display = "none";
    } else {
        // If we already have a result, show it, else show the trigger bar
        if (aiResult) {
            resultContainer.style.display = "flex";
            triggerSection.style.display = "none";
        } else {
            resultContainer.style.display = "none";
            triggerSection.style.display = "block";
        }
    }
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
        
        if (data.event === "show") {
            document.body.classList.toggle("settings-only", !!data.settingsOnly);
            selectedText = data.text ? data.text.trim() : "";
            
            // Clean up previous AI results on fresh trigger
            aiResult = "";
            aiOutput.innerHTML = "Đang chờ yêu cầu...";
            diffOutput.innerHTML = "";
            resultContainer.style.display = "none";
            btnShowDiff.style.display = "none";
            switchResultTab("normal");
            
            // Set text preview
            if (selectedText) {
                sourcePreview.textContent = selectedText;
                sourcePreview.classList.remove("empty");
                translateSourcePreview.textContent = selectedText;
                translateSourcePreview.classList.remove("empty");
                
                // If text is bôi đen (selected), default to "Rewrite" tab
                switchTab("rewrite");
            } else {
                sourcePreview.textContent = buildEmptyText();
                sourcePreview.classList.add("empty");
                translateSourcePreview.textContent = buildEmptyText();
                translateSourcePreview.classList.add("empty");
                
                // If NO text is bôi đen, default to "Write" (Soạn thảo) since we want to compose
                switchTab("write");
            }
            
            // Open settings directly if requested (e.g. from tray icon click)
            if (data.settingsDirectly) {
                switchTab("settings");
            } else {
                promptInput.value = "";
                promptInput.focus();
            }
        } 
        else if (data.event === "startup_status") {
            document.getElementById("system-startup").checked = data.enabled;
        }
        else if (data.event === "hotkey_status") {
            currentHotkeyText = data.hotkey || currentHotkeyText;
            if (hotkeyInput) hotkeyInput.value = currentHotkeyText;
            if (!selectedText) {
                sourcePreview.textContent = buildEmptyText();
                translateSourcePreview.textContent = buildEmptyText();
            }
        }
        else if (data.event === "hotkey_error") {
            if (hotkeyError) {
                hotkeyError.textContent = data.message || "Phím tắt không hợp lệ.";
            }
        }
    });
}

// Allow dragging the window from any non-interactive empty space
document.addEventListener("mousedown", event => {
    const target = event.target;
    // Don't drag if clicking buttons, inputs, select boxes, textareas, links, scrollable previews or selectable result areas
    if (!target || target.closest("button, input, select, textarea, a, svg, path, .selection-preview, .result-text-area, .diff-view, .settings-group, #ai-output, #diff-output, #source-preview, #translate-source-preview")) return;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "start_drag" });
    }
});

// Trigger Generation
document.getElementById("btn-generate").addEventListener("click", startAIProcess);
document.getElementById("btn-retry").addEventListener("click", startAIProcess);

async function startAIProcess() {
    // Show loading
    resultContainer.style.display = "flex";
    triggerSection.style.display = "none";
    
    aiOutput.innerHTML = `
        <div class="stream-loading-dots">
            <span>Đang viết, chờ xíu✍️</span>
            <span class="stream-dot"></span>
            <span class="stream-dot"></span>
            <span class="stream-dot"></span>
        </div>`;
    
    // Disable buttons during generation
    toggleButtonsDisabled(true);
    
    // Build Prompt
    let prompt = "";
    const activeTab = currentTab === "settings" ? lastActiveTextTab : currentTab;
    
    const tone = document.getElementById("param-tone").value;
    const format = document.getElementById("param-format").value;
    const length = document.getElementById("param-length").value;
    const customPrompt = promptInput.value.trim();
    const targetLang = document.getElementById("param-lang").value;
    
    if (activeTab === "rewrite") {
        if (!selectedText) {
            aiOutput.innerHTML = "<span style='color: #F87171;'>Lỗi: Không tìm thấy văn bản đã chọn. Vui lòng đóng cửa sổ này, bôi đen văn bản cần viết lại bên ngoài, rồi nhấn phím tắt.</span>";
            toggleButtonsDisabled(false);
            return;
        }
        
        prompt = `Bạn là một trợ lý viết lách chuyên nghiệp. Hãy viết lại (rewrite) đoạn văn sau đây:
- Tone giọng yêu cầu: ${getToneText(tone)}
- Định dạng yêu cầu: ${getFormatText(format)}
- Độ dài yêu cầu: ${getLengthText(length)}
${customPrompt ? `- Yêu cầu đặc biệt bổ sung: "${customPrompt}"` : ""}

Đoạn văn gốc cần viết lại:
        """
        ${selectedText}
        """

    Hãy CHỈ trả về nội dung đã được viết lại trực tiếp. Giữ nguyên ngôn ngữ gốc và định dạng (xuống dòng, gạch đầu dòng, tiêu đề). Tuyệt đối không thêm lời giới thiệu, lời kết, không giải thích lý do thay đổi và không bọc trong markdown codeblock.`;
    } 
    else if (activeTab === "write") {
        if (!customPrompt) {
            aiOutput.innerHTML = "<span style='color: #F87171;'>Lỗi: Vui lòng nhập nội dung gợi ý (Prompt) hoặc chủ đề bạn muốn soạn thảo.</span>";
            toggleButtonsDisabled(false);
            return;
        }
        
        prompt = `Bạn là một trợ lý viết lách chuyên nghiệp. Hãy soạn thảo một bài viết/văn bản mới theo yêu cầu sau:
        - Chủ đề/Yêu cầu soạn thảo: "${customPrompt}"
- Tone giọng yêu cầu: ${getToneText(tone)}
- Định dạng yêu cầu: ${getFormatText(format)}
- Độ dài yêu cầu: ${getLengthText(length)}
${selectedText ? `- Văn bản gợi ý bổ trợ ngữ cảnh (Context): "${selectedText}"` : ""}

        Hãy CHỈ trả về nội dung đã soạn thảo hoàn chỉnh trực tiếp. Tuyệt đối không thêm lời giới thiệu, lời kết, không giải thích lý do và không bọc trong markdown codeblock.`;
    } 
    else if (activeTab === "translate") {
        if (!selectedText) {
            aiOutput.innerHTML = "<span style='color: #F87171;'>Lỗi: Không tìm thấy văn bản cần dịch. Vui lòng đóng cửa sổ, bôi đen văn bản cần dịch bên ngoài, rồi nhấn phím tắt.</span>";
            toggleButtonsDisabled(false);
            return;
        }
        
        prompt = `Hãy dịch chính xác đoạn văn sau đây sang ${targetLang}:
"""
${selectedText}
"""

Hãy CHỈ trả về nội dung bản dịch trực tiếp. Giữ nguyên định dạng đoạn văn, tuyệt đối không giải thích, không thêm lời chào hay bọc trong markdown codeblock.`;
    }

    // Call API (Streaming)
    try {
        aiResult = "";
        console.log("Generated Prompt:", prompt);
        if (settings.activeProvider === "gemini") {
            await streamGemini(prompt);
        } else {
            await streamOpenAI(prompt);
        }
        
        // Show Diff button if we did a Rewrite or Translation and have selected text to compare
        if (selectedText && (activeTab === "rewrite" || activeTab === "translate")) {
            btnShowDiff.style.display = "inline-block";
            // Pre-calculate diff in background
            calculateAndRenderDiff();
        } else {
            btnShowDiff.style.display = "none";
        }
        
    } catch (error) {
        aiOutput.innerHTML = `<span style='color: #F87171;'>Lỗi kết nối API: ${error.message}<br>Vui lòng kiểm tra lại API Key hoặc tên Model trong mục Cài đặt.</span>`;
    } finally {
        toggleButtonsDisabled(false);
    }
}

// Gemini API Streaming Implementation
async function streamGemini(prompt) {
    if (!settings.geminiKey) {
        throw new Error("Chưa nhập Gemini API Key. Hãy vào phần Cài đặt ở góc trên bên phải.");
    }
    
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${settings.geminiModel}:streamGenerateContent?key=${settings.geminiKey}`;
    
    const response = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            contents: [{
                parts: [{ text: prompt }]
            }]
        })
    });

    if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const errMsg = errorData.error?.message || `HTTP ${response.status}`;
        throw new Error(errMsg);
    }

    aiOutput.innerHTML = ""; // Clear loader
    const reader = response.body.getReader();
    const decoder = new TextDecoder("utf-8");
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        buffer += decoder.decode(value, { stream: true });
        
        // Gemini stream chunks come as JSON array elements in one huge array: [ {...}, {...} ]
        // Let's parse them iteratively by finding matching JSON objects inside the buffer
        let boundaryIndex;
        while ((boundaryIndex = findJsonEnd(buffer)) !== -1) {
            let jsonStr = buffer.substring(0, boundaryIndex).trim();
            buffer = buffer.substring(boundaryIndex);
            
            // Clean up comma at start of chunk if it exists
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
                    // JSON chunk might be incomplete, let's append it back and wait for more data
                    buffer = jsonStr + buffer;
                    break;
                }
            }
        }
    }
    
    // Process remaining buffer
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
        } catch(e) {}
    }
    
    renderStreamingText(aiResult, true); // Render final completed text without cursor
}

// Simple JSON boundary finder for streaming
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
            if (char === "{") {
                depth++;
            } else if (char === "}") {
                depth--;
                if (depth === 0) {
                    return i + 1; // End of JSON object
                }
            }
        }
    }
    return -1;
}

// OpenAI API Streaming Implementation
async function streamOpenAI(prompt) {
    if (!settings.openaiKey) {
        throw new Error("Chưa nhập OpenAI API Key. Hãy vào phần Cài đặt ở góc trên bên phải.");
    }
    
    const url = "https://api.openai.com/v1/chat/completions";
    
    const response = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${settings.openaiKey}`
        },
        body: JSON.stringify({
            model: settings.openaiModel,
            messages: [{ role: "user", content: prompt }],
            stream: true
        })
    });

    if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const errMsg = errorData.error?.message || `HTTP ${response.status}`;
        throw new Error(errMsg);
    }

    aiOutput.innerHTML = ""; // Clear loader
    const reader = response.body.getReader();
    const decoder = new TextDecoder("utf-8");
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        
        // Keep the last partial line in the buffer
        buffer = lines.pop();
        
        for (const line of lines) {
            const cleanLine = line.trim();
            if (!cleanLine) continue;
            if (cleanLine === "data: [DONE]") continue;
            
            if (cleanLine.startsWith("data: ")) {
                try {
                    const parsed = JSON.parse(cleanLine.substring(6));
                    const chunkText = parsed.choices?.[0]?.delta?.content || "";
                    if (chunkText) {
                        aiResult += chunkText;
                        renderStreamingText(aiResult);
                    }
                } catch (e) {
                    console.error("Error parsing OpenAI stream chunk", e);
                }
            }
        }
    }
    
    renderStreamingText(aiResult, true); // Render final completed text without cursor
}

function renderStreamingText(text, isFinished = false) {
    // Format paragraph breaks elegantly as HTML
    let formatted = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    formatted = formatted.replace(/\n/g, "<br>");
    
    if (isFinished) {
        aiOutput.innerHTML = formatted;
    } else {
        aiOutput.innerHTML = formatted + '<span class="stream-cursor"></span>';
    }
    
    // Auto-scroll output container as content flows in
    aiOutput.scrollTop = aiOutput.scrollHeight;
}

// Translations parameter descriptors
function getToneText(tone) {
    const tones = {
        professional: "Chuyên nghiệp, chuẩn mực, lịch sự, khoa học",
        casual: "Thân thiện, gần gũi, giao tiếp hàng ngày, tự nhiên",
        academic: "Học thuật, trang trọng, sử dụng từ ngữ nghiên cứu, chuẩn cấu trúc",
        creative: "Sáng tạo, bay bổng, sinh động, dùng nhiều liên tưởng phóng khoáng",
        humorous: "Hài hước, dí dỏm, tạo tiếng cười nhẹ nhàng, dễ chịu",
        direct: "Thẳng thắn, ngắn gọn, súc tích, đi thẳng vào vấn đề chính"
    };
    return tones[tone] || tone;
}

function getFormatText(format) {
    const formats = {
        paragraph: "Đoạn văn trôi chảy, liền mạch",
        bullets: "Danh sách gạch đầu dòng, trình bày các ý chính rõ ràng",
        email: "Bức thư điện tử (Email) chuyên nghiệp có tiêu đề, mở đầu và kết thư",
        short: "Tin nhắn ngắn gọn, súc tích dành cho chat mạng xã hội hoặc SMS",
        essay: "Bài luận văn/bài viết chi tiết, đầy đủ luận điểm sâu sắc"
    };
    return formats[format] || format;
}

function getLengthText(length) {
    const lengths = {
        short: "Ngắn gọn, súc tích nhất có thể",
        medium: "Độ dài vừa phải, cân đối đầy đủ thông tin",
        long: "Chi tiết, mở rộng thông tin, viết dài và đầy đủ hơn"
    };
    return lengths[length] || length;
}

function toggleButtonsDisabled(disabled) {
    document.getElementById("btn-generate").disabled = disabled;
    document.getElementById("btn-retry").disabled = disabled;
    document.getElementById("btn-replace").disabled = disabled;
    tabs.forEach(btn => {
        if (btn.getAttribute("data-tab") !== "settings") btn.disabled = disabled;
    });
}

// Result vs Diff Tab Toggle
btnShowResult.addEventListener("click", () => switchResultTab("normal"));
btnShowDiff.addEventListener("click", () => switchResultTab("diff"));

function switchResultTab(tabName) {
    if (tabName === "normal") {
        btnShowResult.classList.add("active");
        btnShowDiff.classList.remove("active");
        normalOutputWrapper.style.display = "block";
        diffOutputWrapper.style.display = "none";
    } else {
        btnShowResult.classList.remove("active");
        btnShowDiff.classList.add("active");
        normalOutputWrapper.style.display = "none";
        diffOutputWrapper.style.display = "block";
    }
}

// Action Buttons
document.getElementById("btn-replace").addEventListener("click", () => {
    if (!aiResult) return;
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "paste",
            text: aiResult
        });
    }
});

// Settings Save Trigger
document.getElementById("save-settings-btn").addEventListener("click", saveSettings);


// Premium LCS Word-by-Word Diff Algorithm
function calculateAndRenderDiff() {
    if (!selectedText || !aiResult) return;
    
    // Split into words while retaining spaces/newlines to make formatting beautiful
    // Regex splits by word boundaries or whitespace
    const oldWords = selectedText.split(/(\s+)/);
    const newWords = aiResult.split(/(\s+)/);
    
    // Filter empty values from splitting
    const oldFiltered = oldWords.filter(w => w !== "");
    const newFiltered = newWords.filter(w => w !== "");
    
    const diff = computeLcsDiff(oldFiltered, newFiltered);
    
    // Render diff to HTML
    let html = "";
    diff.forEach(token => {
        const textEscaped = token.text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
        if (token.type === "ins") {
            if (textEscaped.trim() === "") {
                html += textEscaped; // don't bubble highlights on raw white spaces
            } else {
                html += `<span class="diff-ins">${textEscaped}</span>`;
            }
        } else if (token.type === "del") {
            if (textEscaped.trim() === "") {
                // do nothing for deleted white space
            } else {
                html += `<span class="diff-del">${textEscaped}</span>`;
            }
        } else {
            // Normal word
            if (textEscaped === "\n") {
                html += "<br>";
            } else {
                html += `<span class="diff-normal">${textEscaped}</span>`;
            }
        }
    });
    
    diffOutput.innerHTML = html;
}

// Longest Common Subsequence backtracking diff generator
function computeLcsDiff(oldList, newList) {
    const m = oldList.length;
    const n = newList.length;
    
    // Create DP table
    const dp = Array(m + 1).fill(0).map(() => Array(n + 1).fill(0));
    
    // Fill table
    for (let i = 1; i <= m; i++) {
        for (let j = 1; j <= n; j++) {
            // Case-insensitive/strict whitespace compare
            const wordA = oldList[i - 1].trim().toLowerCase();
            const wordB = newList[j - 1].trim().toLowerCase();
            const isMatch = (wordA === wordB && wordA !== "") || (oldList[i-1] === newList[j-1]);
            
            if (isMatch) {
                dp[i][j] = dp[i - 1][j - 1] + 1;
            } else {
                dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
            }
        }
    }
    
    // Backtrack to assemble diff tokens
    let i = m;
    let j = n;
    const result = [];
    
    while (i > 0 || j > 0) {
        if (i > 0 && j > 0) {
            const wordA = oldList[i - 1].trim().toLowerCase();
            const wordB = newList[j - 1].trim().toLowerCase();
            const isMatch = (wordA === wordB && wordA !== "") || (oldList[i-1] === newList[j-1]);
            
            if (isMatch) {
                result.unshift({ type: "normal", text: newList[j - 1] });
                i--;
                j--;
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

// Initialize on document ready
document.addEventListener("DOMContentLoaded", () => {
    loadSettings();
    // Advanced options toggle
    const advancedWrapper = document.querySelector(".advanced-options-wrapper");
    const advancedToggleBtn = document.getElementById("advanced-toggle-btn");

    if (advancedWrapper && advancedToggleBtn) {
        advancedToggleBtn.addEventListener("click", () => {
            advancedWrapper.classList.toggle("open");
        });
    }


    // Setup premium keypress recorder for hotkey input
    if (hotkeyInput) {
        hotkeyInput.addEventListener("keydown", event => {
            event.preventDefault();
            event.stopPropagation();

            const key = event.key;
            
            // Ignore standalone modifier keydown events (we will capture them when a combination key is pressed)
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

            // Build key combination
            let parts = [];
            if (event.ctrlKey) parts.push("Ctrl");
            if (event.shiftKey) parts.push("Shift");
            if (event.altKey) parts.push("Alt");
            if (event.metaKey) parts.push("Win");

            // Map event.key to our friendly display names
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
                if (keyDisplay === "Delete") keyDisplay = "Delete";
                if (keyDisplay === "Insert") keyDisplay = "Insert";
                if (keyDisplay === "PageUp") keyDisplay = "PageUp";
                if (keyDisplay === "PageDown") keyDisplay = "PageDown";
            }

            parts.push(keyDisplay);
            hotkeyInput.value = parts.join("+");
        });
    }

    const params = new URLSearchParams(window.location.search);
    if (params.get("mode") === "settings")
    {
        document.body.classList.add("settings-only");
        switchTab("settings");
    }
});
