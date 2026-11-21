using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        [Serializable]
        private sealed class CollectionFile { public string[] entries; }

        private static string collectionPath;
        private static HashSet<string> collection;
        private static bool collectionDirty;

        private static HashSet<string> OpenCollection()
        {
            string path = Path.Combine(SaveDirectory, "collection.json");
            if (collection != null && path == collectionPath) return collection;
            var entries = File.Exists(path) ? JsonUtility.FromJson<CollectionFile>(File.ReadAllText(path)).entries
                : Array.Empty<string>();
            collection = new HashSet<string>(entries);
            collectionPath = path;
            collectionDirty = false;
            return collection;
        }

        private static void Remember(IEnumerable<string> keys)
        {
            try
            {
                var known = OpenCollection();
                foreach (var key in keys) collectionDirty |= known.Add(key);
                if (!collectionDirty) return;
                Directory.CreateDirectory(SaveDirectory);
                string temp = collectionPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(new CollectionFile { entries = known.OrderBy(k => k).ToArray() }));
                if (File.Exists(collectionPath)) File.Replace(temp, collectionPath, null);
                else File.Move(temp, collectionPath);
                collectionDirty = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("图鉴保存失败：" + error.Message);
            }
        }

        public static string MapCollectionKey(int id) => id >= HexWorldMap.Id(-HexWorldMap.Radius, -HexWorldMap.Radius)
            ? "wild:" + id % 3 : "item:" + id;

        public static IReadOnlyCollection<string> ReadCollection(ScenarioData scenario)
        {
            // Old life records retain starting possessions and successful story choices.
            IEnumerable<string> ArchivedItems()
            {
                foreach (var saved in WorldTimelineSaveStore.Load(SavePath))
                {
                    foreach (int id in saved.Record.InitialItems) yield return MapCollectionKey(id);
                    foreach (var formation in saved.Record.Expedition)
                        foreach (var item in ExpeditionEquipment.ArchivedPossessions(formation))
                            yield return (item.LegacyConsumable ? "legacy:" : "gear:") + item.Id;
                    foreach (var choice in saved.Record.StoryChoices)
                        if (scenario.Stories.Options.TryGetValue(choice.OptionId, out var option) && option.ItemId > 0)
                            yield return MapCollectionKey(option.ItemId);
                }
            }
            Remember(ArchivedItems());
            return OpenCollection();
        }

        private void RecordCollection()
        {
            if (returnToMenu == null) return;
            var state = simulation.State;
            IEnumerable<string> Held()
            {
                foreach (var species in state.DefeatedGuardianSpecies) yield return "monster:" + species;
                foreach (var id in state.AcquiredPlayerItems.Concat(state.Actors[state.PlayerActorId].Items)) yield return MapCollectionKey(id);
                var party = state.Expedition;
                if (party?.Equipment.Enabled != true) yield break;
                var gear = party.Equipment;
                var actors = party.LivingRoles.Select(party.FieldId).Prepend(party.LeaderId);
                if (gear.Modern)
                {
                    foreach (var item in gear.Instances) yield return "gear:" + item.DefinitionId;
                    foreach (var actor in actors)
                        if (gear.Innate(actor) is ArtifactDefinition item) yield return "gear:" + item.Id;
                }
                else
                {
                    foreach (var item in ExpeditionEquipment.Catalog)
                        if (gear.Count(item.Id) > 0 || actors.Any(actor => Enumerable.Range(0, 3)
                            .Any(slot => gear.Equipped(actor, slot) == item.Id)))
                            yield return (item.Id >= 9 ? "legacy:" : "gear:") + item.Id;
                }
            }
            Remember(Held());
        }
    }
}
