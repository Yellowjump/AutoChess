using System;
using System.Collections.Generic;
using System.IO;

namespace WorldTimeline
{
    public sealed class SavedLife
    {
        public int Round { get; }
        public LifeRecord Record { get; }

        public SavedLife(int round, LifeRecord record)
        {
            if (round < 1 || round == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(round));
            Round = round;
            Record = record ?? throw new ArgumentNullException(nameof(record));
        }
    }

    public static class WorldTimelineSaveStore
    {
        private const int Signature = 0x574C4946;
        private const int Version = 1;
        public const int Capacity = 15;
        private const int MaxEntriesPerLife = 100000;

        public static IReadOnlyList<SavedLife> Load(string path)
        {
            if (!File.Exists(path)) return Array.Empty<SavedLife>();
            return Decode(File.ReadAllBytes(path));
        }

        public static IReadOnlyList<SavedLife> Decode(byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data, false));
            if (reader.ReadInt32() != Signature || reader.ReadInt32() != Version)
                throw new InvalidDataException("无法识别前世存档版本。");

            int count = ReadCount(reader, Capacity);
            var records = new List<SavedLife>(count);
            for (int i = 0; i < count; i++) records.Add(ReadLife(reader));
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("前世存档含有未识别的数据。");
            return records;
        }

        public static void Save(string path, IReadOnlyList<SavedLife> records)
        {
            if (records.Count > Capacity) throw new ArgumentOutOfRangeException(nameof(records));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, Encode(records));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static byte[] Encode(IReadOnlyList<SavedLife> records)
        {
            if (records.Count > Capacity) throw new ArgumentOutOfRangeException(nameof(records));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(Signature);
                writer.Write(Version);
                writer.Write(records.Count);
                foreach (var entry in records) WriteLife(writer, entry);
            }
            return stream.ToArray();
        }

        private static void WriteLife(BinaryWriter writer, SavedLife entry)
        {
            var life = entry.Record;
            writer.Write(entry.Round);
            writer.Write(life.SourceActorId);
            writer.Write(life.BirthNodeId);
            writer.Write(life.InitialHealth);
            writer.Write(life.InitialAttack);
            writer.Write(life.Lifespan);
            writer.Write(life.InitialLevel);
            WriteTalent(writer, life.Talents.Health);
            WriteTalent(writer, life.Talents.Attack);
            WriteTalent(writer, life.Talents.Lifespan);
            writer.Write(life.EndYear);
            writer.Write(life.EndedInDeath);
            writer.Write(life.InitialItems.Count);
            foreach (int item in life.InitialItems) writer.Write(item);
            writer.Write(life.Actions.Count);
            foreach (var step in life.Actions)
            {
                writer.Write(step.StartYear);
                writer.Write((int)step.Action.Kind);
                writer.Write(step.Action.Years);
                writer.Write(step.Action.TargetNodeId);
            }
        }

        private static SavedLife ReadLife(BinaryReader reader)
        {
            int round = reader.ReadInt32();
            var actor = new ActorState(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            actor.Level = reader.ReadInt32();
            if (actor.Level < 1) throw new InvalidDataException("前世等级无效。");
            actor.Talents = new TalentProfile(ReadTalent(reader), ReadTalent(reader), ReadTalent(reader));
            int year = reader.ReadInt32();
            if (year < 0) throw new InvalidDataException("前世年份无效。");
            bool died = reader.ReadBoolean();
            var items = new int[ReadCount(reader, MaxEntriesPerLife)];
            for (int i = 0; i < items.Length; i++) items[i] = reader.ReadInt32();
            var actions = new RecordedAction[ReadCount(reader, MaxEntriesPerLife)];
            for (int i = 0; i < actions.Length; i++)
            {
                int startYear = reader.ReadInt32();
                var kind = (WorldActionKind)reader.ReadInt32();
                int years = reader.ReadInt32();
                int target = reader.ReadInt32();
                WorldAction action;
                switch (kind)
                {
                    case WorldActionKind.Wait: action = WorldAction.Wait(years); break;
                    case WorldActionKind.Move: action = WorldAction.Move(target, years); break;
                    case WorldActionKind.Cultivate: action = WorldAction.Cultivate(years); break;
                    case WorldActionKind.Disguise: action = WorldAction.Disguise(); break;
                    case WorldActionKind.JoinChosen: action = WorldAction.JoinChosen(); break;
                    default: throw new InvalidDataException("前世行动类型无效。");
                }
                actions[i] = new RecordedAction(startYear, action);
            }
            return new SavedLife(round, new LifeRecord(actor, items, actions, Array.Empty<WorldEvent>(), year, died));
        }

        private static int ReadCount(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("前世存档数量无效。");
            return count;
        }

        private static AttributeTalent ReadTalent(BinaryReader reader) =>
            new AttributeTalent((TalentGrade)reader.ReadInt32(), reader.ReadInt32());

        private static void WriteTalent(BinaryWriter writer, AttributeTalent talent)
        {
            writer.Write((int)talent.Grade);
            writer.Write(talent.Roll);
        }
    }
}
