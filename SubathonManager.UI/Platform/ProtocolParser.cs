using System.Web;
using SubathonManager.Core;
using SubathonManager.Core.Objects;

namespace SubathonManager.UI.Platform;

public static class ProtocolParser
{
    public static ActivationRequest Parse(string[] args)
    {
        if (args.Length == 0)
            return new ActivationRequest(ActivationKind.Unknown, string.Empty);

        var arg = args[0];
        
        if (arg.EndsWith(".smo", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(arg) &&
            !arg.Contains("subathonmanager://oauth", StringComparison.CurrentCultureIgnoreCase))
        {
            Utils.PendingOverlayImportPath = arg;
            return new ActivationRequest(ActivationKind.SmoFile, arg);
        }

        if ((arg.EndsWith(".smw", StringComparison.OrdinalIgnoreCase) ||
             arg.EndsWith(".smwc", StringComparison.OrdinalIgnoreCase)) && File.Exists(arg))
        {
            Utils.PendingWidgetPackImportPath = arg;
            return new ActivationRequest(ActivationKind.SmwFile, arg);
        }

        if (!arg.StartsWith("subathonmanager://"))
            return new ActivationRequest(ActivationKind.Unknown, arg);

        var uri = new Uri(arg);

        if (uri.Host == "oauth")
        {
            var provider = uri.AbsolutePath.TrimStart('/');
            var oauthQuery = HttpUtility.ParseQueryString(uri.Query);

            Utils.PendingOAuthCallback = new OAuthCallback
            {
                Provider = provider,
                AccessToken = oauthQuery["access_token"] ?? "",
                RefreshToken = oauthQuery["refresh_token"] ?? "",
                Code = oauthQuery["code"] ?? "",
                Error = oauthQuery["error"] ?? "",
                ExpiresIn = oauthQuery["expires_in"] ?? "",
                ClientId = oauthQuery["client_id"] ?? ""
            };
            return new ActivationRequest(ActivationKind.OAuth, arg);
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        var url = query["url"];
        if (string.IsNullOrEmpty(url))
            return new ActivationRequest(ActivationKind.Unknown, arg);

        var kind = ClassifyPack(url);
        if (kind == ActivationKind.Unknown) kind = ClassifyPack(uri.AbsolutePath);
        if (kind == ActivationKind.Unknown) kind = ClassifyHost(uri.Host);

        switch (kind)
        {
            case ActivationKind.SmoFile:
                Utils.PendingOverlayImportPath = url;
                return new ActivationRequest(ActivationKind.SmoFile, url);
            case ActivationKind.SmwFile:
                Utils.PendingWidgetPackImportPath = url;
                return new ActivationRequest(ActivationKind.SmwFile, url);
            default:
                return new ActivationRequest(ActivationKind.Unknown, arg);
        }
    }

    private static ActivationKind ClassifyPack(string value)
    {
        var path = value;
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0) path = path[..cut];

        if (path.EndsWith(".smo", StringComparison.OrdinalIgnoreCase))
            return ActivationKind.SmoFile;
        if (path.EndsWith(".smw", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".smwc", StringComparison.OrdinalIgnoreCase))
            return ActivationKind.SmwFile;
        return ActivationKind.Unknown;
    }

    private static ActivationKind ClassifyHost(string host) => host.ToLowerInvariant() switch
    {
        "overlay" or "smo" => ActivationKind.SmoFile,
        "widget" or "widgets" or "smw" or "smwc" or "collection" => ActivationKind.SmwFile,
        _ => ActivationKind.Unknown
    };
}
