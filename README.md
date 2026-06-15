# 🚀 ReWrite AI Desktop App

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/hynady/ReWrite?color=7cc0f4&style=for-the-badge)](https://github.com/hynady/ReWrite)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-blue?style=for-the-badge&logo=windows)](https://github.com/hynady/ReWrite)
[![AI Powered](https://img.shields.io/badge/Power_by-Gemini_%26_OpenAI-orange?style=for-the-badge&logo=google-gemini)](https://github.com/hynady/ReWrite)
[![Speed](https://img.shields.io/badge/Speedup-200%25--300%25-brightgreen?style=for-the-badge&logo=rocket)](https://github.com/hynady/ReWrite)

[![GitHub stars](https://img.shields.io/github/stars/hynady/ReWrite?style=flat-square&color=yellow&logo=github)](https://github.com/hynady/ReWrite/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/hynady/ReWrite?style=flat-square&color=blue&logo=git)](https://github.com/hynady/ReWrite/network/members)
[![GitHub all releases downloads](https://img.shields.io/github/downloads/hynady/ReWrite/total?style=flat-square&color=success&logo=github)](https://github.com/hynady/ReWrite/releases)
[![GitHub contributors](https://img.shields.io/github/contributors/hynady/ReWrite?style=flat-square&color=orange)](https://github.com/hynady/ReWrite/graphs/contributors)
[![GitHub watchers](https://img.shields.io/github/watchers/hynady/ReWrite?style=flat-square&color=teal)](https://github.com/hynady/ReWrite/watchers)
[![GitHub repo size](https://img.shields.io/github/repo-size/hynady/ReWrite?style=flat-square&color=important)](https://github.com/hynady/ReWrite)
[![GitHub last commit](https://img.shields.io/github/last-commit/hynady/ReWrite?style=flat-square&color=lightgrey)](https://github.com/hynady/ReWrite)
[![License: Free Software](https://img.shields.io/badge/License-Free_&_Permissive-green.svg?style=flat-square)](https://github.com/hynady/ReWrite#license)

**ReWrite AI** is a native desktop application designed to deeply integrate artificial intelligence into your daily operating system workflow. It empowers you to optimize text, translate, and compose content instantly across **any active application interface** (MS Word, Browsers, Notepad, Excel, Discord, Slack, etc.) without the need to switch tabs or open browser windows.

Say goodbye to tedious, repetitive workflows: *Highlight ➔ Copy ➔ Open AI Web Tab ➔ Paste ➔ Wait ➔ Copy Output ➔ Switch Back ➔ Paste*. With ReWrite AI, everything happens directly at your cursor tip!

## 🎥 Video Demonstration

Click the preview image below to watch the complete setup and features guide on YouTube:

[![Watch the video](https://img.youtube.com/vi/bM_BwH0zYKY/maxresdefault.jpg)](https://www.youtube.com/watch?v=bM_BwH0zYKY)

## 🔥 Core Features

### ✍️ Rewrite Mode
* **1-Click Replace:** Fix typos, enhance grammar, and improve phrasing, then instantly overwrite the original text with a single click.
* **Granular Customization:**
  * **Tones:** Professional, Friendly, Academic, Creative, Humorous, and more.
  * **Formats:** Paragraph, Bullet Points, Email, Chat Message, Long-form Article, etc.
  * **Lengths:** Short, Medium, or Longer depending on your needs.
* **Diff View:** Visually compare the original text against the AI's modifications before applying changes.

### 🌐 Translate Mode
* Break language barriers instantly. Translate highlighted text into numerous major languages (English, Vietnamese, Chinese, Japanese, Korean, French, German, etc.) with ultra-low latency powered by next-gen LLMs.

### 📝 Compose Mode
* Generate high-quality content from scratch. Simply input a brief prompt or core idea, select your preferred tone/format, and let the AI draft a complete piece for you in seconds.
<img width="1376" height="768" alt="main-pop_up_screenshot" src="https://github.com/user-attachments/assets/2f6d7a59-4be4-4c32-a933-c5b34b9fbc4d" />
<img width="1376" height="768" alt="setting_screenshot" src="https://github.com/user-attachments/assets/4556cd6b-6181-4a5d-aaa6-a207b2815c20" />
## ⚡ UI/UX Workflow Breakthrough

* **⚡ Zero Context Switching:** The ultra-lightweight pop-up window triggers right over your current workspace via global hotkeys, preserving your mental focus and boosting overall productivity by an estimated **200% - 300%**.
* **💰 Bring Your Own Key (BYOK):** Connect directly via your personal API keys (Google AI Studio, OpenAI). Minimize subscription overhead and leverage fast, cost-effective models like `gemini-1.5-flash-lite` virtually for free.
* **📜 Local History Management:** Easily review, manage, and retrieve past text transformations without risking data loss.

## 🛠️ Installation Guide

### Windows

1. Download the latest version executable from the [Releases](https://github.com/hynady/ReWrite/releases) section.
2. Run the `ReWrite <version>.exe` installer (the application will self-configure within seconds).
3. Launch the app and head over to **Settings**:
   * Paste your personal **API Key** (Generated from Google AI Studio or OpenAI).
   * Choose your preferred model line (e.g., `gemini-1.5-flash-lite`).
   * Map your global hotkeys and toggle *Launch on Windows Startup* if desired.

### macOS

The macOS version is built from source (native Swift/AppKit shell sharing the same UI):

```bash
cd macos
./build-app.sh
mv dist/ReWrite.app /Applications/
open /Applications/ReWrite.app
```

ReWrite runs as a **menu bar app** (look for the logo near the clock — there is no Dock icon). On first use macOS will ask for **Accessibility** access, which is required to capture the selected text and paste the result back. See [`macos/README.md`](macos/README.md) for full setup, usage and troubleshooting.

## ⌨️ Default Hotkeys

**Windows**

* **`Ctrl + Shift + Space`**: Toggle the main ReWrite AI control dashboard.
* **`Ctrl + Shift + A`**: Instantly trigger AI processing for the currently highlighted text.

**macOS**

* **`Cmd + Shift + A`**: Trigger the AI popup for the currently highlighted text.

*(You can fully customize these keybindings within the application settings to match your personal habits).*

## 🤝 Contributing

Contributions, bug reports (Issues), and feature requests (Pull Requests) are highly appreciated! 

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 🏬 Microsoft Store

I am thrilled to announce that my app is now officially live on the Microsoft Store! 🥳

👉 **Get it here:** https://apps.microsoft.com/detail/9P97QQ6048RX

## 📊 Star History

Thank you for supporting this project! Drop a ⭐ if you find this tool helpful.

[![Star History Chart](https://api.star-history.com/svg?repos=hynady/ReWrite&type=Date)](https://star-history.com/#hynady/ReWrite&Date)

## ☕ Support Me

ReWrite AI is built with passion and distributed entirely for free to help streamline everyone's workflows. If this tool saves you time and brings value to your day, consider supporting my ongoing independent open-source efforts!

[![Ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/hnduy)

## 📄 License

Copyright (C) 2026 Huỳnh Nam Duy. All rights reserved.

This software is provided **"as-is"**, completely **free and open** for anyone to use for any purpose, including commercial applications, as well as to alter and redistribute under the following permissive conditions:

* **Retain Copyrights:** All redistributions in source code or binary form must retain the original copyright notices and conditions.
* **No Misrepresentation:** You must not claim that you wrote the original software.
* **Mark Modifications:** Any altered/modified versions must be plainly marked as modified and not misrepresented as the original software.

Please check the full [ReWrite License](LICENSE) file in this repository for detailed legal terms.

🎨 *Developed & Maintained with ❤️ by hynady*
