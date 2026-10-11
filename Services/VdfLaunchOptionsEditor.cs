using System.Text;

namespace ModFrame.Services;

internal sealed class VdfLaunchOptionsEditor
{
    private readonly string _content;
    private readonly List<VdfNode> _rootNodes;

    public VdfLaunchOptionsEditor(string content)
    {
        _content = content;
        var tokens = Tokenize(content);
        var index = 0;
        _rootNodes = ParseNodes(tokens, ref index, out _);
    }

    public string GetLaunchOptions(string appId)
    {
        var app = FindAppSection(appId);
        return app?.Children
            .OfType<VdfProperty>()
            .FirstOrDefault(node => node.Key.Equals("LaunchOptions", StringComparison.OrdinalIgnoreCase))
            ?.ValueToken.Value ?? string.Empty;
    }

    public string SetLaunchOptions(string appId, string launchOptions)
    {
        var apps = FindAppsSection()
            ?? throw new InvalidDataException("The Steam apps section was not found in localconfig.vdf.");
        var app = apps.Children
            .OfType<VdfSection>()
            .FirstOrDefault(node => node.Key.Equals(appId, StringComparison.OrdinalIgnoreCase));

        if (app is null)
        {
            return InsertAppSection(apps, appId, launchOptions);
        }

        var property = app.Children
            .OfType<VdfProperty>()
            .FirstOrDefault(node => node.Key.Equals("LaunchOptions", StringComparison.OrdinalIgnoreCase));

        if (property is not null)
        {
            return Replace(
                property.ValueToken.Start + 1,
                property.ValueToken.End - 1,
                Escape(launchOptions));
        }

        var newline = DetectNewline();
        var insertionPoint = LineStart(app.CloseToken.Start);
        var indentation = LineIndent(app.CloseToken.Start) + "\t";
        var newProperty = $"{indentation}\"LaunchOptions\"\t\t\"{Escape(launchOptions)}\"{newline}";
        return Insert(insertionPoint, newProperty);
    }

    public string RemoveLaunchOptions(string appId)
    {
        var app = FindAppSection(appId);
        var property = app?.Children
            .OfType<VdfProperty>()
            .FirstOrDefault(node => node.Key.Equals("LaunchOptions", StringComparison.OrdinalIgnoreCase));

        if (property is null)
        {
            return _content;
        }

        var start = LineStart(property.KeyToken.Start);
        var end = LineEnd(property.ValueToken.End);
        return Replace(start, end, string.Empty);
    }

    private VdfSection? FindAppSection(string appId)
    {
        return FindAppsSection()?.Children
            .OfType<VdfSection>()
            .FirstOrDefault(node => node.Key.Equals(appId, StringComparison.OrdinalIgnoreCase));
    }

    private VdfSection? FindAppsSection()
    {
        return FindSectionAtPath(
            _rootNodes,
            ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps"]);
    }

    private static VdfSection? FindSectionAtPath(IReadOnlyList<VdfNode> nodes, IReadOnlyList<string> path)
    {
        VdfSection? current = null;
        var level = nodes;
        foreach (var part in path)
        {
            current = level
                .OfType<VdfSection>()
                .FirstOrDefault(node => node.Key.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                return null;
            }

            level = current.Children;
        }

        return current;
    }

    private string InsertAppSection(VdfSection apps, string appId, string launchOptions)
    {
        var newline = DetectNewline();
        var insertionPoint = LineStart(apps.CloseToken.Start);
        var sectionIndent = LineIndent(apps.CloseToken.Start) + "\t";
        var propertyIndent = sectionIndent + "\t";
        var block = new StringBuilder()
            .Append(sectionIndent).Append('"').Append(appId).Append('"').Append(newline)
            .Append(sectionIndent).Append('{').Append(newline)
            .Append(propertyIndent).Append("\"LaunchOptions\"\t\t\"")
            .Append(Escape(launchOptions)).Append('"').Append(newline)
            .Append(sectionIndent).Append('}').Append(newline)
            .ToString();
        return Insert(insertionPoint, block);
    }

    private string Insert(int index, string value)
    {
        return _content[..index] + value + _content[index..];
    }

    private string Replace(int start, int end, string value)
    {
        return _content[..start] + value + _content[end..];
    }

    private int LineStart(int index)
    {
        var newline = _content.LastIndexOf('\n', Math.Max(0, index - 1));
        return newline < 0 ? 0 : newline + 1;
    }

    private int LineEnd(int index)
    {
        var newline = _content.IndexOf('\n', index);
        return newline < 0 ? _content.Length : newline + 1;
    }

    private string LineIndent(int index)
    {
        var start = LineStart(index);
        var length = 0;
        while (start + length < _content.Length &&
               _content[start + length] is ' ' or '\t')
        {
            length++;
        }

        return _content.Substring(start, length);
    }

    private string DetectNewline()
    {
        return _content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static List<VdfNode> ParseNodes(
        IReadOnlyList<VdfToken> tokens,
        ref int index,
        out VdfToken closeToken)
    {
        var nodes = new List<VdfNode>();
        closeToken = default;

        while (index < tokens.Count)
        {
            if (tokens[index].Kind == VdfTokenKind.CloseBrace)
            {
                closeToken = tokens[index++];
                break;
            }

            if (tokens[index].Kind != VdfTokenKind.String)
            {
                index++;
                continue;
            }

            var keyToken = tokens[index++];
            if (index >= tokens.Count)
            {
                break;
            }

            if (tokens[index].Kind == VdfTokenKind.OpenBrace)
            {
                var openToken = tokens[index++];
                var children = ParseNodes(tokens, ref index, out var childClose);
                nodes.Add(new VdfSection(keyToken.Value, keyToken, openToken, childClose, children));
            }
            else if (tokens[index].Kind == VdfTokenKind.String)
            {
                nodes.Add(new VdfProperty(keyToken.Value, keyToken, tokens[index++]));
            }
            else
            {
                index++;
            }
        }

        return nodes;
    }

    private static List<VdfToken> Tokenize(string content)
    {
        var tokens = new List<VdfToken>();
        var index = 0;

        while (index < content.Length)
        {
            if (char.IsWhiteSpace(content[index]))
            {
                index++;
                continue;
            }

            if (content[index] == '/' && index + 1 < content.Length && content[index + 1] == '/')
            {
                index += 2;
                while (index < content.Length && content[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            if (content[index] == '[')
            {
                while (index < content.Length && content[index] != ']')
                {
                    index++;
                }

                index = Math.Min(content.Length, index + 1);
                continue;
            }

            if (content[index] == '{' || content[index] == '}')
            {
                var kind = content[index] == '{' ? VdfTokenKind.OpenBrace : VdfTokenKind.CloseBrace;
                tokens.Add(new VdfToken(kind, index, index + 1, content[index].ToString()));
                index++;
                continue;
            }

            if (content[index] == '"')
            {
                var start = index++;
                var value = new StringBuilder();
                while (index < content.Length)
                {
                    if (content[index] == '"')
                    {
                        index++;
                        break;
                    }

                    if (content[index] == '\\' && index + 1 < content.Length)
                    {
                        var escaped = content[index + 1];
                        value.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            _ => escaped
                        });
                        index += 2;
                        continue;
                    }

                    value.Append(content[index++]);
                }

                tokens.Add(new VdfToken(VdfTokenKind.String, start, index, value.ToString()));
                continue;
            }

            var bareStart = index;
            while (index < content.Length &&
                   !char.IsWhiteSpace(content[index]) &&
                   content[index] is not '{' and not '}')
            {
                index++;
            }

            tokens.Add(new VdfToken(
                VdfTokenKind.String,
                bareStart,
                index,
                content[bareStart..index]));
        }

        return tokens;
    }

    private abstract record VdfNode(string Key, VdfToken KeyToken);

    private sealed record VdfProperty(string Name, VdfToken NameToken, VdfToken ValueToken)
        : VdfNode(Name, NameToken);

    private sealed record VdfSection(
        string Name,
        VdfToken NameToken,
        VdfToken OpenToken,
        VdfToken CloseToken,
        IReadOnlyList<VdfNode> Children)
        : VdfNode(Name, NameToken);

    private readonly record struct VdfToken(VdfTokenKind Kind, int Start, int End, string Value);

    private enum VdfTokenKind
    {
        String,
        OpenBrace,
        CloseBrace
    }
}
