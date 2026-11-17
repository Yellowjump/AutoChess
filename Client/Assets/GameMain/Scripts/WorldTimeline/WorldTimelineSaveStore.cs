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
        private const int Version = 11;
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
            if (reader.ReadInt32() != Signature)
                throw new InvalidDataException("无法识别前世存档版本。");
            int version = reader.ReadInt32();
            if (version < 1 || version > Version) throw new InvalidDataException("无法识别前世存档版本。");

            int count = ReadCount(reader, Capacity);
            var records = new List<SavedLife>(count);
            for (int i = 0; i < count; i++) records.Add(ReadLife(reader, version));
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
            writer.Write(life.StoryChoices.Count);
            foreach (var choice in life.StoryChoices)
            { writer.Write(choice.EventId); writer.Write(choice.Occurrence); writer.Write(choice.OptionId); }
            writer.Write(life.GuardianDeathNodeId.HasValue);
            if (life.GuardianDeathNodeId is int nodeId) writer.Write(nodeId);
            writer.Write(life.RitualBreakYear.HasValue);
            if (life.RitualBreakYear is int breakYear) writer.Write(breakYear);
            writer.Write(life.SluiceOpeningYear.HasValue);
            if (life.SluiceOpeningYear is int openingYear) writer.Write(openingYear);
            writer.Write(life.NorthRelayRestoredYear.HasValue);
            if (life.NorthRelayRestoredYear is int restoreYear) writer.Write(restoreYear);
            writer.Write(life.ContractRevokedYear.HasValue);
            if (life.ContractRevokedYear is int contractYear) writer.Write(contractYear);
            writer.Write(life.Expedition.Count);
            foreach (var step in life.Expedition)
            {
                writer.Write(step.Year); writer.Write(step.NodeId); writer.Write(step.Battle);
                writer.Write(step.Progression); writer.Write(step.CloseTaunt);
                writer.Write(step.Recruited.Count); foreach (int role in step.Recruited) writer.Write(role);
                foreach (int cell in step.Cells) writer.Write(cell);
                writer.Write(step.Equipment.Count);
                foreach(int value in step.Equipment)writer.Write(value);
            }
        }

        private static SavedLife ReadLife(BinaryReader reader, int version)
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
            var choices = new List<RecordedStoryChoice>();
            if (version >= 2)
                for (int i = ReadCount(reader, MaxEntriesPerLife); i > 0; i--)
                    choices.Add(new RecordedStoryChoice(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
            int? deathNode = version >= 3 && reader.ReadBoolean() ? reader.ReadInt32() : null;
            int? breakYear = version >= 4 && reader.ReadBoolean() ? reader.ReadInt32() : null;
            if (breakYear is int recordedYear && (!died || recordedYear < 1 || recordedYear != year))
                throw new InvalidDataException("前世破仪年份无效。");
            int? sluiceYear = version >= 5 && reader.ReadBoolean() ? reader.ReadInt32() : null;
            if (sluiceYear is int opened && (opened < 1 || opened > year))
                throw new InvalidDataException("前世开闸年份无效。");
            int? northRelayYear = version >= 6 && reader.ReadBoolean() ? reader.ReadInt32() : null;
            if (northRelayYear is int restored && (restored < 1 || restored > year))
                throw new InvalidDataException("前世还名年份无效。");
            int? contractYear = version >= 7 && reader.ReadBoolean() ? reader.ReadInt32() : null;
            if (contractYear is int revoked && (revoked < 1 || revoked > year))
                throw new InvalidDataException("前世撤契年份无效。");
            var expedition = new List<RecordedExpedition>();
            int expeditionCount = version >= 8 ? ReadCount(reader, MaxEntriesPerLife) : 0;
            for (int i = 0; i < expeditionCount; i++)
            {
                int stepYear = reader.ReadInt32(), node = reader.ReadInt32(), battle = reader.ReadInt32();
                bool progression = reader.ReadBoolean(), closeTaunt = reader.ReadBoolean();
                var roles = new int[ReadCount(reader, 4)];
                for (int r = 0; r < roles.Length; r++) roles[r] = reader.ReadInt32();
                var cells = new int[6]; for (int c = 0; c < 6; c++) cells[c] = reader.ReadInt32();
                if (stepYear > year || (i > 0 && stepYear < expedition[i - 1].Year))
                    throw new InvalidDataException("前世队伍记录年份无效。");
                var equipment=new int[version>=9?ReadCount(reader,ExpeditionEquipment.MaxSnapshot):0];
                for(int e=0;e<equipment.Length;e++)equipment[e]=reader.ReadInt32();
                expedition.Add(new RecordedExpedition(stepYear, node, battle, progression, closeTaunt, roles, cells, equipment));
            }
            return new SavedLife(round, new LifeRecord(actor, items, actions, Array.Empty<WorldEvent>(), year, died, choices,
                deathNode, breakYear, sluiceYear, northRelayYear, contractYear, expedition));
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
