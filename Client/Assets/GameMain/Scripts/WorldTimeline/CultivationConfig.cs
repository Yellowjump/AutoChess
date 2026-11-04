using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTimeline
{
    public enum TalentGrade { S, A, B, C }
    public enum GrowthAttribute { Health, Attack, Lifespan }

    public sealed class AttributeTalent
    {
        public TalentGrade Grade { get; }
        public int Roll { get; }
        public AttributeTalent(TalentGrade grade, int roll)
        {
            if (!Enum.IsDefined(typeof(TalentGrade), grade) || roll < 0 || roll > 10000) throw new ArgumentOutOfRangeException();
            Grade = grade; Roll = roll;
        }
    }

    public sealed class TalentProfile
    {
        public AttributeTalent Health { get; }
        public AttributeTalent Attack { get; }
        public AttributeTalent Lifespan { get; }
        public TalentProfile(AttributeTalent health, AttributeTalent attack, AttributeTalent lifespan)
        { Health = health ?? throw new ArgumentNullException(nameof(health)); Attack = attack ?? throw new ArgumentNullException(nameof(attack)); Lifespan = lifespan ?? throw new ArgumentNullException(nameof(lifespan)); }

        public static TalentProfile Generate(int seed)
        {
            uint state = unchecked((uint)seed) ^ 0x9e3779b9u;
            AttributeTalent Next()
            {
                state = unchecked(state * 1664525u + 1013904223u);
                var grade = (TalentGrade)((state >> 16) % 4);
                state = unchecked(state * 1664525u + 1013904223u);
                return new AttributeTalent(grade, (int)(state % 10001));
            }
            return new TalentProfile(Next(), Next(), Next());
        }
    }

    public sealed class AttributeRange
    {
        public int Min { get; }
        public int Max { get; }
        public AttributeRange(int min, int max)
        {
            if (min < 1 || max < min) throw new ArgumentException("Attribute range must be positive and ordered.");
            Min = min; Max = max;
        }
        public int Sample(AttributeTalent talent) => checked(Min + (int)(((long)Max - Min + 1) * talent.Roll / 10001));
        public int SampleAtLevel(AttributeTalent talent, int level)
        {
            if (level == 1) return Sample(talent);
            uint hash = unchecked((uint)talent.Roll ^ (uint)level * 0x9e3779b9u);
            hash ^= hash >> 16; hash = unchecked(hash * 0x85ebca6bu); hash ^= hash >> 13;
            return checked(Min + (int)(((long)Max - Min + 1) * (hash % 10001) / 10001));
        }
        public override string ToString() => Min + "～" + Max;
    }

    public sealed class CultivationLevel
    {
        public int Level { get; }
        public long ExperienceToNext { get; }
        private readonly Dictionary<GrowthAttribute, AttributeRange[]> ranges;
        internal CultivationLevel(int level, long experience, Dictionary<GrowthAttribute, AttributeRange[]> ranges)
        { Level = level; ExperienceToNext = experience; this.ranges = ranges; }
        public AttributeRange Range(GrowthAttribute attribute, TalentGrade grade) => ranges[attribute][(int)grade];
    }

    public sealed class CultivationGrowthRow
    {
        public int Level { get; }
        public long Experience { get; }
        public GrowthAttribute Attribute { get; }
        public AttributeRange[] Ranges { get; }
        public CultivationGrowthRow(int level, long experience, GrowthAttribute attribute, params AttributeRange[] ranges)
        { Level = level; Experience = experience; Attribute = attribute; Ranges = ranges; }
    }

    public sealed class CultivationConfig
    {
        public IReadOnlyList<CultivationLevel> Levels { get; }
        private CultivationConfig(List<CultivationLevel> levels) { Levels = levels.AsReadOnly(); }
        public CultivationLevel GetLevel(int level)
        {
            if (level < 1 || level > Levels.Count) throw new ArgumentOutOfRangeException(nameof(level));
            return Levels[level - 1];
        }

        // Level 1 is the birth range; subsequent rows are independent level-up increments.
        public AttributeRange CumulativeRange(int level, GrowthAttribute attribute, TalentGrade grade)
        {
            GetLevel(level);
            int min = 0, max = 0;
            for (int i = 1; i <= level; i++)
            {
                var range = GetLevel(i).Range(attribute, grade);
                min = checked(min + range.Min); max = checked(max + range.Max);
            }
            return new AttributeRange(min, max);
        }

        // CSV parsing is retained for test fixtures. Runtime configuration uses typed data-table rows.
        public static CultivationConfig ParseCsv(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var rows = new List<CultivationGrowthRow>();
            foreach (var raw in csv.Split('\n'))
            {
                string line = raw.Trim().TrimStart('\ufeff');
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("Level,")) continue;
                string[] cells = line.Split(',');
                if (cells.Length != 11) throw new FormatException("Growth CSV needs 11 columns: " + line);
                int level = int.Parse(cells[0], CultureInfo.InvariantCulture);
                long cost = long.Parse(cells[1], CultureInfo.InvariantCulture);
                if (level < 1 || cost < 0) throw new FormatException("Invalid growth level or cost.");
                var attribute = (GrowthAttribute)Enum.Parse(typeof(GrowthAttribute), cells[2].Trim());
                if (!Enum.IsDefined(typeof(GrowthAttribute), attribute)) throw new FormatException("Unknown growth attribute.");
                var ranges = new AttributeRange[4];
                for (int i = 0; i < 4; i++) ranges[i] = new AttributeRange(int.Parse(cells[3 + i * 2], CultureInfo.InvariantCulture), int.Parse(cells[4 + i * 2], CultureInfo.InvariantCulture));
                rows.Add(new CultivationGrowthRow(level, cost, attribute, ranges));
            }
            return FromRows(rows);
        }

        public static CultivationConfig FromRows(IEnumerable<CultivationGrowthRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var costs = new Dictionary<int, long>();
            var data = new Dictionary<int, Dictionary<GrowthAttribute, AttributeRange[]>>();
            foreach (var row in rows)
            {
                int level = row.Level;
                long cost = row.Experience;
                var attribute = row.Attribute;
                var ranges = row.Ranges;
                if (level < 1 || cost < 0 || !Enum.IsDefined(typeof(GrowthAttribute), attribute))
                    throw new FormatException("Invalid growth level, cost or attribute.");
                if (ranges == null || ranges.Length != 4 || ranges.Any(range => range == null))
                    throw new FormatException("Growth rows require four talent ranges.");
                if (!data.ContainsKey(level)) { data.Add(level, new Dictionary<GrowthAttribute, AttributeRange[]>()); costs.Add(level, cost); }
                if (costs[level] != cost || data[level].ContainsKey(attribute)) throw new FormatException("Duplicate attribute or inconsistent level cost.");
                for (int i = 1; i < 4; i++)
                    if (ranges[i].Min > ranges[i - 1].Min || ranges[i].Max > ranges[i - 1].Max)
                        throw new FormatException("Talent ranges must descend from S to C.");
                data[level].Add(attribute, (AttributeRange[])ranges.Clone());
            }
            var levels = new List<CultivationLevel>();
            foreach (int level in data.Keys.OrderBy(x => x))
            {
                if (level != levels.Count + 1 || data[level].Count != 3) throw new FormatException("Levels must be consecutive and have all three attributes.");
                if ((level == data.Count) != (costs[level] == 0)) throw new FormatException("Only the final level must have zero next-level experience.");
                var entry = new CultivationLevel(level, costs[level], data[level]);
                levels.Add(entry);
            }
            if (levels.Count == 0) throw new FormatException("Growth table is empty.");
            var config = new CultivationConfig(levels);
            foreach (var level in levels)
                foreach (GrowthAttribute attribute in Enum.GetValues(typeof(GrowthAttribute)))
                    for (int grade = 0; grade < 3; grade++)
                        if (config.CumulativeRange(level.Level, attribute, (TalentGrade)grade).Min <=
                            config.CumulativeRange(level.Level, attribute, (TalentGrade)(grade + 1)).Max)
                            throw new FormatException("Cumulative ranges must keep higher talent grades strictly ahead at level " + level.Level + ".");
            return config;
        }
    }
}
