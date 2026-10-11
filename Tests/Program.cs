using ModFrame.Services;

const string appId = "123456";
const string originalOption = "PROTON_LOG=1 %command%";
const string requestedOption = "WINEDLLOVERRIDES=\"winhttp.dll=n,b\" %command%";

var sample = """
    "UserLocalConfigStore"
    {
        "Software"
        {
            "Valve"
            {
                "Steam"
                {
                    "apps"
                    {
                        "123456"
                        {
                            "LastPlayed"		"10"
                            "LaunchOptions"		"PROTON_LOG=1 %command%"
                        }
                    }
                    "UnrelatedValue"		"must remain unchanged"
                }
            }
        }
    }
    """;

var editor = new VdfLaunchOptionsEditor(sample);
AssertEqual(originalOption, editor.GetLaunchOptions(appId), "read existing option");

var updated = editor.SetLaunchOptions(appId, requestedOption);
AssertEqual(
    requestedOption,
    new VdfLaunchOptionsEditor(updated).GetLaunchOptions(appId),
    "replace existing option");
AssertContains(updated, "must remain unchanged", "preserve unrelated configuration");

var removed = new VdfLaunchOptionsEditor(updated).RemoveLaunchOptions(appId);
AssertEqual(string.Empty, new VdfLaunchOptionsEditor(removed).GetLaunchOptions(appId), "remove option");
AssertContains(removed, "LastPlayed", "preserve other app values");

var inserted = new VdfLaunchOptionsEditor(sample).SetLaunchOptions("999999", requestedOption);
AssertEqual(
    requestedOption,
    new VdfLaunchOptionsEditor(inserted).GetLaunchOptions("999999"),
    "insert option for a new app section");

Console.WriteLine("All VDF launch-option tests passed.");

static void AssertEqual(string expected, string actual, string scenario)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Failed to {scenario}. Expected '{expected}', received '{actual}'.");
    }
}

static void AssertContains(string content, string expected, string scenario)
{
    if (!content.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Failed to {scenario}.");
    }
}
