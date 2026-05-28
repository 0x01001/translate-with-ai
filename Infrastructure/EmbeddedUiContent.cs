using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace ReWrite
{
    /// <summary>
    /// Serves embedded UI resources (HTML, CSS, JS, images) to WebView2
    /// via a virtual https://rewrite.local/* origin.
    /// Moved to Infrastructure/ — no logic changes.
    /// </summary>
    internal static class EmbeddedUiContent
    {
        private const string ResourcePrefix = "ReWrite.ui.";
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        private static readonly Lazy<HashSet<string>> ResourceNames = new(() =>
            Assembly.GetManifestResourceNames().ToHashSet(StringComparer.OrdinalIgnoreCase));

        public static void ConfigureWebView(CoreWebView2 webView)
        {
            webView.AddWebResourceRequestedFilter("https://rewrite.local/*", CoreWebView2WebResourceContext.All);
            webView.WebResourceRequested += WebView_WebResourceRequested;
        }

        public static ImageSource? LoadImageSource(string resourcePath)
        {
            using Stream? stream = OpenResourceStream(resourcePath);
            if (stream == null) return null;

            return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        public static System.Drawing.Icon? LoadDrawingIcon(string resourcePath)
        {
            using Stream? stream = OpenResourceStream(resourcePath);
            if (stream == null) return null;

            return new System.Drawing.Icon(stream);
        }

        private static void WebView_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (sender is not CoreWebView2 webView) return;

            if (!TryGetResourceForRequest(e.Request.Uri, out byte[] content, out string contentType))
            {
                content = Encoding.UTF8.GetBytes("Not Found");
                contentType = "text/plain; charset=utf-8";
                e.Response = webView.Environment.CreateWebResourceResponse(
                    new MemoryStream(content), 404, "Not Found", $"Content-Type: {contentType}");
                return;
            }

            e.Response = webView.Environment.CreateWebResourceResponse(
                new MemoryStream(content), 200, "OK", $"Content-Type: {contentType}");
        }

        private static bool TryGetResourceForRequest(string requestUri, out byte[] content, out string contentType)
        {
            var uri = new Uri(requestUri);
            string resourcePath = uri.AbsolutePath.Trim('/');

            if (string.IsNullOrWhiteSpace(resourcePath) || resourcePath == "/")
                resourcePath = "popup.html";

            using Stream? stream = OpenResourceStream(resourcePath);
            if (stream == null)
            {
                content = Array.Empty<byte>();
                contentType = "text/plain; charset=utf-8";
                return false;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            content = memoryStream.ToArray();
            contentType = GetContentType(resourcePath);
            return true;
        }

        private static Stream? OpenResourceStream(string resourcePath)
        {
            string normalizedPath = resourcePath.Replace('/', '.').Replace('\\', '.');
            string exactName = ResourcePrefix + normalizedPath;

            Stream? stream = Assembly.GetManifestResourceStream(exactName);
            if (stream != null) return stream;

            string suffix = "." + normalizedPath;
            string? matchedName = ResourceNames.Value.FirstOrDefault(name =>
                name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            return matchedName == null ? null : Assembly.GetManifestResourceStream(matchedName);
        }

        private static string GetContentType(string resourcePath)
        {
            string extension = Path.GetExtension(resourcePath).ToLowerInvariant();
            return extension switch
            {
                ".html"          => "text/html; charset=utf-8",
                ".css"           => "text/css; charset=utf-8",
                ".js"            => "application/javascript; charset=utf-8",
                ".json"          => "application/json; charset=utf-8",
                ".png"           => "image/png",
                ".jpg" or ".jpeg"=> "image/jpeg",
                ".gif"           => "image/gif",
                ".svg"           => "image/svg+xml",
                ".ico"           => "image/x-icon",
                ".woff"          => "font/woff",
                ".woff2"         => "font/woff2",
                _                => "application/octet-stream"
            };
        }
    }
}
