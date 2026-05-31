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
    "Return ONLY the rewritten content directly. Maintain the original language and formatting (line breaks, bullet points, headings). Do NOT include any introductions, conclusions, explanations, or markdown code blocks."
].join("\n");

const WRITE_PROMPT_TEMPLATE = [
    "You are a professional writing assistant. Draft a new piece of text based on the following requirements:",
    "- Topic/Prompt: \"{{customPrompt}}\"",
    "- Tone: {{tone}}",
    "- Format: {{format}}",
    "- Length: {{length}}",
    "{{contextLine}}",
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