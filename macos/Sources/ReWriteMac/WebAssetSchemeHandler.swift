import WebKit

/// Port of Infrastructure/EmbeddedUiContent.cs.
/// Serves the bundled web UI on the rewrite://local/* origin
/// (WKWebView cannot intercept https://, so the Windows build's
/// https://rewrite.local URLs are rewritten to this scheme at build time).
final class WebAssetSchemeHandler: NSObject, WKURLSchemeHandler {
    static let scheme = "rewrite"
    static let origin = "rewrite://local"

    private let root = AppPaths.webRoot.standardizedFileURL

    func webView(_ webView: WKWebView, start urlSchemeTask: WKURLSchemeTask) {
        guard let url = urlSchemeTask.request.url else { return }

        var path = url.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        if path.isEmpty { path = "popup.html" }

        let fileURL = root.appendingPathComponent(path).standardizedFileURL
        guard fileURL.path.hasPrefix(root.path),
              let data = try? Data(contentsOf: fileURL)
        else {
            respond(urlSchemeTask, url: url, status: 404,
                    mime: "text/plain; charset=utf-8", body: Data("Not Found".utf8))
            return
        }

        respond(urlSchemeTask, url: url, status: 200,
                mime: Self.contentType(for: fileURL.pathExtension), body: data)
    }

    func webView(_ webView: WKWebView, stop urlSchemeTask: WKURLSchemeTask) {}

    private func respond(_ task: WKURLSchemeTask, url: URL, status: Int, mime: String, body: Data) {
        let response = HTTPURLResponse(
            url: url,
            statusCode: status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": mime, "Access-Control-Allow-Origin": "*"]
        )!
        task.didReceive(response)
        task.didReceive(body)
        task.didFinish()
    }

    private static func contentType(for ext: String) -> String {
        switch ext.lowercased() {
        case "html":         return "text/html; charset=utf-8"
        case "css":          return "text/css; charset=utf-8"
        case "js":           return "application/javascript; charset=utf-8"
        case "json":         return "application/json; charset=utf-8"
        case "png":          return "image/png"
        case "jpg", "jpeg":  return "image/jpeg"
        case "gif":          return "image/gif"
        case "svg":          return "image/svg+xml"
        case "ico":          return "image/x-icon"
        case "woff":         return "font/woff"
        case "woff2":        return "font/woff2"
        default:             return "application/octet-stream"
        }
    }
}
