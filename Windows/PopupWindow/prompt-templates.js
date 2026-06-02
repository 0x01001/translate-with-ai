const REWRITE_PROMPT_TEMPLATE = [
    "You are a professional writing assistant. Rewrite the following text based on these specifications:",
    "- Tone: {{tone}}",
    "- Format: {{format}}",
    "- Length: {{length}}",
    "{{customPromptLine}}",
    "Original text to rewrite:",
    "\"\"\"",
    "{{selectedText}}",
    "\"\"\"",
    "IMPORTANT: You MUST preserve the original language of the input text. If the input is in Vietnamese, Chinese, Japanese, or any other language, the rewrite MUST be in that same language. Never translate or switch to English unless explicitly requested.",
    "Return ONLY the rewritten content directly. Maintain the original formatting (line breaks, bullet points, headings). Do NOT include any introductions, conclusions, explanations, or markdown code blocks."
].join("\n");

const WRITE_PROMPT_TEMPLATE = [
    "You are a professional writing assistant. Draft a new piece of text based on the following requirements:",
    "- Topic/Prompt: \"{{customPrompt}}\"",
    "- Tone: {{tone}}",
    "- Format: {{format}}",
    "- Length: {{length}}",
    "{{contextLine}}",
    "IMPORTANT: You MUST match the language of the user's topic/prompt and context text. If the topic or context is in Vietnamese, Chinese, Japanese, or any other language, write the output in that same language. Never switch to English unless explicitly requested.",
    "Return ONLY the completed text directly. Do NOT include any introductions, conclusions, explanations, or markdown code blocks."
].join("\n");

const TRANSLATE_PROMPT_TEMPLATE = [
    "Accurately translate the following text into {{targetLang}}:",
    "\"\"\"",
    "{{selectedText}}",
    "\"\"\"",
    "Return ONLY the translated content directly. Preserve the original paragraph formatting. Do NOT include any explanations, greetings, or markdown code blocks."
].join("\n");

window.ReWritePromptTemplates = Object.freeze({
    rewrite: REWRITE_PROMPT_TEMPLATE,
    write: WRITE_PROMPT_TEMPLATE,
    translate: TRANSLATE_PROMPT_TEMPLATE
});