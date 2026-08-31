using System.Text.Json;
using Techne.Loom.Common.ReleaseSet;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var authority = ParseAuthority(options["authority"]);
            var phase = ParsePhase(options["phase"]);
            var requiresPackageMetadata = authority == LoomReleaseSetAuthorityMode.CheckIn
                || phase == LoomReleaseSetValidationPhase.PostPublish
                || phase == LoomReleaseSetValidationPhase.PostPublishPackageClosure;
            var report = await LoomReleaseSetValidator.ValidateAsync(new LoomReleaseSetValidationRequest
            {
                RepositoryRoot = options["repository-root"],
                ManifestPath = options.TryGetValue("manifest", out var manifest) ? manifest : "release-set.json",
                Channel = options["channel"],
                AuthorityMode = authority,
                Phase = phase,
                CandidateVersion = options.TryGetValue("candidate-version", out var candidateVersion) ? candidateVersion : null,
                PackageRoot = options.TryGetValue("package-root", out var packageRoot) ? packageRoot : null,
                PackageMetadataSource = requiresPackageMetadata ? new LoomNuGetPackageMetadataSource() : null,
            });

            if (options.TryGetValue("report", out var reportPath))
            {
                var fullReportPath = Path.GetFullPath(reportPath);
                var reportDirectory = Path.GetDirectoryName(fullReportPath);
                if (!string.IsNullOrWhiteSpace(reportDirectory))
                {
                    Directory.CreateDirectory(reportDirectory);
                }

                var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                await File.WriteAllTextAsync(fullReportPath, reportJson + Environment.NewLine);
            }

            Console.WriteLine(report.ToDiagnosticString());
            return report.IsValid ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Release-set validator could not start: {exception.Message}");
            return 2;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Expected a value after option '{name}'.");
            }

            var key = name.Substring(2);
            if (key.Length == 0 || options.ContainsKey(key))
            {
                throw new ArgumentException($"Option '{name}' is duplicated or invalid.");
            }

            options[key] = args[++index];
        }

        Require(options, "repository-root");
        Require(options, "channel");
        Require(options, "authority");
        Require(options, "phase");
        return options;
    }

    private static LoomReleaseSetAuthorityMode ParseAuthority(string value)
        => value.ToLowerInvariant() switch
        {
            "check-in" or "checkin" => LoomReleaseSetAuthorityMode.CheckIn,
            "release" => LoomReleaseSetAuthorityMode.Release,
            _ => throw new ArgumentException($"Unsupported authority '{value}'. Use check-in or release."),
        };

    private static LoomReleaseSetValidationPhase ParsePhase(string value)
        => value.ToLowerInvariant() switch
        {
            "pre-publish" or "prepublish" => LoomReleaseSetValidationPhase.PrePublish,
            "pre-publish-package-closure" or "prepublish-package-closure" => LoomReleaseSetValidationPhase.PrePublishPackageClosure,
            "post-publish" or "postpublish" => LoomReleaseSetValidationPhase.PostPublish,
            "post-publish-package-closure" or "package-closure" => LoomReleaseSetValidationPhase.PostPublishPackageClosure,
            _ => throw new ArgumentException($"Unsupported phase '{value}'. Use pre-publish, pre-publish-package-closure, post-publish-package-closure, or post-publish."),
        };

    private static void Require(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The '--{name}' option is required.");
        }
    }
}
