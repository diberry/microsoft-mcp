using System.Text.Json;
using System.Text.RegularExpressions;
using Shared;

namespace NaturalLanguageGenerator;


public static class TextCleanup
{
    private static string? nlParametersPath = null;

    private static string? textReplacerParametersPath = null;

    public static string? TextReplacerParametersFilePath => textReplacerParametersPath;
    public static string? ParametersFilePath => nlParametersPath;

    public static MappedParameter[]? mappedParameters { get; private set; }

    private static Dictionary<string, string>? mappedParametersDict;
    // Precompiled regex for multi-key replacement (constructed in LoadFiles)
    private static Regex? replacerRegex;

    public static bool LoadFiles(List<string> RequiredFiles)
    {
        try
        {
            if (RequiredFiles == null || RequiredFiles.Count == 0)
            {
                Console.WriteLine("Warning: RequiredFiles list is null or empty. Returning null.");
                return false;
            }

            List<MappedParameter> combinedParameters = new();

            for (int i = 0; i < RequiredFiles.Count; i++)
            {
                var file = RequiredFiles[i];

                if (file.IndexOf("nl-parameters.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nlParametersPath = file;
                }
                if (file.IndexOf("static-text-replacement.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    textReplacerParametersPath = file;
                }
            }

            if (File.Exists(nlParametersPath))
            {
                var nlJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(nlParametersPath));
                if (nlJsonArray != null)
                {
                    combinedParameters.AddRange(nlJsonArray);
                }
            }
            else
            {
                Console.WriteLine($"Warning: nl-parameters.json file not found at '{nlParametersPath}'.");
            }

            if (File.Exists(textReplacerParametersPath))
            {
                var textReplaceJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(textReplacerParametersPath));
                if (textReplaceJsonArray != null)
                {
                    combinedParameters.AddRange(textReplaceJsonArray);
                }
            }
            else
            {
                Console.WriteLine($"Warning: static-text-replacement.json file not found at '{textReplacerParametersPath}'.");
            }

            // Combine and deduplicate parameters based on the 'Parameter' property
            mappedParameters = combinedParameters
                .GroupBy(p => p.Parameter, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();

            // Build a case-insensitive dictionary for O(1) lookups
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mp in mappedParameters)
            {
                if (!string.IsNullOrEmpty(mp.Parameter))
                {
                    dict[mp.Parameter] = mp.NaturalLanguage ?? string.Empty;
                }
            }
            mappedParametersDict = dict;

            // Pre-build a single compiled alternation regex (longer keys first) for faster replacements.
            // Wrap each key with lookarounds so it matches only as a full word (not inside another token).
            var keys = dict.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToArray();

            if (keys.Length > 0)
            {
                var patternParts = keys.Select(k => $"(?<![A-Za-z0-9_-]){Regex.Escape(k)}(?![A-Za-z0-9_-])");
                var pattern = string.Join("|", patternParts);
                replacerRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            else
            {
                replacerRegex = null;
            }

            return true;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameters: {ex.Message}");
            return false;
        }
    }

    private static string[] SplitAndTransformProgrammaticName(string programmaticName)
    {
        // Split the programmatic name into words
        var words = programmaticName.Split('-');

        // Replace known `bad text` for Docs requirements
        for (int i = 1; i < words.Length; i++)
        {
            if (mappedParametersDict != null && mappedParametersDict.TryGetValue(words[i], out var naturalLanguageValue))
            {
                words[i] = naturalLanguageValue;
            }
        }

        // Capitalize the first word
        words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);

        return words;
    }

    public static string NormalizeParameter(string programmaticName)
    {


        if (mappedParametersDict != null && mappedParametersDict.TryGetValue(programmaticName, out var naturalLanguageName))
        {
            Console.WriteLine($"Found natural language name for '{programmaticName}': {naturalLanguageName}");
            return naturalLanguageName;
        }

        // Word isn't in list - break it apart and fix it
        var words = SplitAndTransformProgrammaticName(programmaticName);

        for (int i = 0; i < words.Length; i++)
        {
            words[i] = ReplaceStaticText(words[i]);
        }

        Console.WriteLine($"Converted '{programmaticName}' to natural language: {string.Join(" ", words)}");

        return string.Join(" ", words);
    }
    public static string ReplaceStaticText(string text)
    {
        if (string.IsNullOrEmpty(text) || mappedParametersDict == null || mappedParametersDict.Count == 0)
        {
            return text;
        }

        // If we have a precompiled regex, do a single-pass replacement with a MatchEvaluator.
        if (replacerRegex != null)
        {
            return replacerRegex.Replace(text, m =>
            {
                // mappedParametersDict is case-insensitive; match.Value is the matched key
                if (mappedParametersDict.TryGetValue(m.Value, out var replacement))
                {
                    return replacement ?? string.Empty;
                }
                return m.Value;
            });
        }

        // Fallback: ordered per-key regex replacement (less efficient) with boundary lookarounds
        var orderedKeys = mappedParametersDict.Keys
            .Where(k => !string.IsNullOrEmpty(k))
            .OrderByDescending(k => k.Length)
            .ToList();

        foreach (var key in orderedKeys)
        {
            var replacement = mappedParametersDict[key] ?? string.Empty;
            var pattern = $"(?<![A-Za-z0-9_-]){Regex.Escape(key)}(?![A-Za-z0-9_-])";
            text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return text;
    }
}
