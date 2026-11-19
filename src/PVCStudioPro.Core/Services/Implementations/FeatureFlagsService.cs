using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class FeatureFlagsService : IFeatureFlagsService
    {
        private readonly Dictionary<string, string> _featureStatuses = new(StringComparer.OrdinalIgnoreCase);

        public FeatureFlagsService(string? customFeaturesJsonPath = null)
        {
            LoadStatuses(customFeaturesJsonPath);
        }

        private void LoadStatuses(string? customPath)
        {
            // 1. Explicit custom file path
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                try
                {
                    ParseJsonString(File.ReadAllText(customPath));
                    EnsureDefaults();
                    return;
                }
                catch { }
            }

            // 2. Embedded Assembly Resource (guaranteed in built binaries without docs/ folder)
            try
            {
                var assembly = typeof(FeatureFlagsService).Assembly;
                // Look for resource ending with FEATURES.json
                string? resName = null;
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("FEATURES.json", StringComparison.OrdinalIgnoreCase))
                    {
                        resName = name;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(resName))
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        ParseJsonString(json);
                        EnsureDefaults();
                        return;
                    }
                }
            }
            catch { }

            // 3. Fallback: Search filesystem for docs/FEATURES.json
            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, "docs", "FEATURES.json");
                if (File.Exists(candidate))
                {
                    try
                    {
                        ParseJsonString(File.ReadAllText(candidate));
                        EnsureDefaults();
                        return;
                    }
                    catch { }
                    break;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            EnsureDefaults();
        }

        private void ParseJsonString(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp) &&
                        el.TryGetProperty("status", out var statusProp))
                    {
                        string id = idProp.GetString() ?? string.Empty;
                        string status = statusProp.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(id))
                        {
                            _featureStatuses[id] = status;
                        }
                    }
                }
            }
        }

        private void EnsureDefaults()
        {
            // Fallback defaults for unfinished features if not loaded from JSON
            if (!_featureStatuses.ContainsKey("F-THERMAL-RECEIPT"))
            {
                _featureStatuses["F-THERMAL-RECEIPT"] = "UI-only";
            }
            if (!_featureStatuses.ContainsKey("F-BATCH-SPOOL"))
            {
                _featureStatuses["F-BATCH-SPOOL"] = "Partial";
            }
        }

        public bool IsFeatureEnabled(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId)) return false;

            if (_featureStatuses.TryGetValue(featureId, out var status))
            {
                return string.Equals(status, "Working", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public string GetFeatureStatus(string featureId)
        {
            if (_featureStatuses.TryGetValue(featureId, out var status))
            {
                return status;
            }
            return "Missing";
        }

        public IReadOnlyDictionary<string, string> GetAllFeatureStatuses()
        {
            return _featureStatuses;
        }
    }
}
