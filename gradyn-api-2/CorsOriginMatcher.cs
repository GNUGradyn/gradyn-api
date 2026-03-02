namespace gradyn_api_2;

/// <summary>
/// Matches CORS origins against config patterns like:
/// - https://example.com
/// - https://*.example.com
/// </summary>
internal sealed class HostMatcher
{
    private readonly HashSet<string> _exactHosts;
    private readonly List<string> _wildcardSuffixes;
    private readonly bool _enforceProtocol;
    private readonly HashSet<string> _allowedSchemes;

    private HostMatcher(
        HashSet<string> exactHosts,
        List<string> wildcardSuffixes,
        bool enforceProtocol,
        HashSet<string> allowedSchemes)
    {
        _exactHosts = exactHosts;
        _wildcardSuffixes = wildcardSuffixes;
        _enforceProtocol = enforceProtocol;
        _allowedSchemes = allowedSchemes;
    }

    public static HostMatcher Compile(
        IEnumerable<string> patterns,
        bool enforceProtocol,
        IEnumerable<string> allowedSchemes)
    {
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wildcards = new List<string>();

        foreach (var raw in patterns ?? Array.Empty<string>())
        {
            var p = (raw ?? "").Trim();
            if (p.Length == 0) continue;

            if (p.StartsWith("*.", StringComparison.Ordinal))
                wildcards.Add(p.Substring(1)); // ".gradyn.com"
            else
                exact.Add(p);
        }

        var schemes = new HashSet<string>(
            (allowedSchemes ?? Array.Empty<string>())
                .Select(s => s.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase
        );

        return new HostMatcher(exact, wildcards, enforceProtocol, schemes);
    }

    public bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        if (_enforceProtocol)
        {
            if (_allowedSchemes.Count == 0)
                return false;

            if (!_allowedSchemes.Contains(uri.Scheme))
                return false;
        }

        var host = uri.Host;

        if (_exactHosts.Contains(host))
            return true;

        foreach (var suffix in _wildcardSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                host.Length > suffix.Length)
            {
                return true;
            }
        }

        return false;
    }
}