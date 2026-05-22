using System.Text.RegularExpressions;
using QuestJournal.Core.Model;

namespace QuestJournal.Core.Parsing;

public sealed class JournalParser
{
    private static readonly Regex BulletRegex = new(
        @"^(?<indent>[\t ]*)(?:-|\d+\.)\s+(?:\[(?<mark>.)\]\s+)?(?<text>.*)$",
        RegexOptions.Compiled);

    public JournalDocument Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var frontmatter = new List<string>();
        var days = new List<DaySection>();

        DayBuilder? currentDay = null;
        CategoryBuilder? currentCategory = null;

        int i = 0;

        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            i = 1;
            while (i < lines.Length && lines[i].Trim() != "---")
            {
                frontmatter.Add(lines[i]);
                i++;
            }
            if (i < lines.Length) i++;
        }

        for (; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = raw.TrimEnd();
            var lineNumber = i + 1;

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.TrimStart() == "---")
            {
                CloseCategory(currentDay, currentCategory);
                CloseDay(days, currentDay);
                currentCategory = null;
                currentDay = null;
                continue;
            }

            if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
            {
                CloseCategory(currentDay, currentCategory);
                CloseDay(days, currentDay);
                currentCategory = null;
                currentDay = new DayBuilder(trimmed.Substring(2).Trim(), lineNumber);
                continue;
            }

            if (trimmed.StartsWith("## ") && !trimmed.StartsWith("### "))
            {
                CloseCategory(currentDay, currentCategory);
                if (currentDay is null)
                {
                    currentDay = new DayBuilder(string.Empty, lineNumber);
                }
                currentCategory = new CategoryBuilder(trimmed.Substring(3).Trim(), lineNumber);
                continue;
            }

            if (trimmed.StartsWith("#")) continue;

            var match = BulletRegex.Match(raw);
            if (!match.Success) continue;

            if (currentCategory is null) continue;

            var rawIndent = NormalizeIndent(match.Groups["indent"].Value);
            var markGroup = match.Groups["mark"];
            var status = markGroup.Success ? MapMark(markGroup.Value) : QuestStatus.Comment;
            var text = match.Groups["text"].Value.Trim();

            currentCategory.AddQuest(rawIndent, status, text, lineNumber);
        }

        CloseCategory(currentDay, currentCategory);
        CloseDay(days, currentDay);

        return new JournalDocument(days, frontmatter);
    }

    public JournalDocument ParseFile(string path) => Parse(File.ReadAllText(path));

    private static int NormalizeIndent(string indent)
    {
        int depth = 0;
        int spaces = 0;
        foreach (var c in indent)
        {
            if (c == '\t')
            {
                depth += 1 + spaces / 4;
                spaces = 0;
            }
            else
            {
                spaces++;
            }
        }
        depth += spaces / 4;
        return depth;
    }

    private static QuestStatus MapMark(string mark) => QuestStatusMarks.FromMark(mark);

    private static void CloseCategory(DayBuilder? day, CategoryBuilder? cat)
    {
        if (day is null || cat is null) return;
        day.Categories.Add(cat.Build());
    }

    private static void CloseDay(List<DaySection> days, DayBuilder? day)
    {
        if (day is null) return;
        days.Add(new DaySection(day.Name, day.Categories, day.LineNumber));
    }

    private sealed class DayBuilder
    {
        public string Name { get; }
        public int LineNumber { get; }
        public List<CategorySection> Categories { get; } = new();

        public DayBuilder(string name, int lineNumber)
        {
            Name = name;
            LineNumber = lineNumber;
        }
    }

    private sealed class CategoryBuilder
    {
        public string Name { get; }
        public int LineNumber { get; }

        private readonly List<MutableQuest> _topLevel = new();
        private readonly List<(int Depth, int RawIndent, MutableQuest Quest)> _stack = new();

        public CategoryBuilder(string name, int lineNumber)
        {
            Name = name;
            LineNumber = lineNumber;
        }

        public void AddQuest(int rawIndent, QuestStatus status, string text, int lineNumber)
        {
            var mq = new MutableQuest(status, text, lineNumber);

            if (_stack.Count == 0)
            {
                mq.Depth = 0;
                _topLevel.Add(mq);
                _stack.Add((0, rawIndent, mq));
                return;
            }

            var topRaw = _stack[^1].RawIndent;

            if (rawIndent > topRaw)
            {
                var parent = _stack[^1];
                mq.Depth = parent.Depth + 1;
                parent.Quest.Children.Add(mq);
                _stack.Add((mq.Depth, rawIndent, mq));
                return;
            }

            while (_stack.Count > 0 && _stack[^1].RawIndent > rawIndent)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            if (_stack.Count == 0)
            {
                mq.Depth = 0;
                _topLevel.Add(mq);
                _stack.Add((0, rawIndent, mq));
                return;
            }

            var current = _stack[^1];
            if (current.RawIndent == rawIndent)
            {
                _stack.RemoveAt(_stack.Count - 1);
                mq.Depth = current.Depth;
                if (_stack.Count == 0)
                {
                    _topLevel.Add(mq);
                }
                else
                {
                    _stack[^1].Quest.Children.Add(mq);
                }
                _stack.Add((mq.Depth, rawIndent, mq));
                return;
            }

            mq.Depth = current.Depth + 1;
            current.Quest.Children.Add(mq);
            _stack.Add((mq.Depth, rawIndent, mq));
        }

        public CategorySection Build()
        {
            var quests = _topLevel.Select(Freeze).ToList();
            return new CategorySection(Name, quests, LineNumber);
        }

        private static Quest Freeze(MutableQuest mq) =>
            new(mq.Status, mq.Text, mq.Children.Select(Freeze).ToList(), mq.Depth, mq.LineNumber);
    }

    private sealed class MutableQuest
    {
        public QuestStatus Status { get; }
        public string Text { get; }
        public int LineNumber { get; }
        public int Depth { get; set; }
        public List<MutableQuest> Children { get; } = new();

        public MutableQuest(QuestStatus status, string text, int lineNumber)
        {
            Status = status;
            Text = text;
            LineNumber = lineNumber;
        }
    }
}
