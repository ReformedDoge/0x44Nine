using Newtonsoft.Json;
using Ninelives_Offline.Utilities;

namespace Ninelives_Offline.Data
{
    internal static class RandomItemIDs
    {
        // Single thread-safe random instance
        private static readonly ThreadLocal<Random> random = new(() =>
            new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

        // Simple lazy loading with minimal overhead
        private static readonly Lazy<DataRoot> dropData = new(() =>
            JsonConvert.DeserializeObject<DataRoot>(
                Helpers.GetEmbeddedResource("Ninelives_Offline.Data.drop_data.json"),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Reuse }
            ));

        private static readonly Lazy<DataRoot> shopData = new(() =>
            JsonConvert.DeserializeObject<DataRoot>(
                Helpers.GetEmbeddedResource("Ninelives_Offline.Data.shop_data.json"),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Reuse }
            ));

        // Simple structs without any extra overhead
        public struct DropList
        {
            public double DropRate { get; set; }
            public int MinDropCount { get; set; }
            public int MaxDropCount { get; set; }
            public bool FixedAllDrop { get; set; }
            public int FullRate { get; set; }
            public List<Group> FixedGroups { get; set; }
            public List<Group> Groups { get; set; }
        }

        public struct Group
        {
            public int CommonGroupID { get; set; }
            public int Rate { get; set; }
            public List<ItemIdSet> IdSets { get; set; }
            public List<long> Ids { get; set; }
        }

        public struct ItemIdSet
        {
            public long Id { get; set; }
            public int StackMax { get; set; }
        }

        public class DataRoot
        {
            public Dictionary<string, DropList> DropLists { get; set; }
            public Dictionary<string, Group> Groups { get; set; }

            public DataRoot()
            {
                DropLists = new Dictionary<string, DropList>();
                Groups = new Dictionary<string, Group>();
            }
        }

        public struct InitItemDataSet
        {
            public readonly int id;
            public readonly long randomCode;
            public readonly int count;

            public InitItemDataSet(int id, long randomCode, int count)
            {
                this.id = id;
                this.randomCode = randomCode;
                this.count = count;
            }
        }

        private static long GenerateRandomCode()
        {
            byte[] array = new byte[8];
            random.Value!.NextBytes(array);
            return BitConverter.ToInt64(array);
        }

        private static DataRoot GetData(bool isShopData) =>
            isShopData ? shopData.Value : dropData.Value;

        private static Group SelectGroup(DropList dropList)
        {
            int threshold = dropList.FullRate + 1;
            int randomValue = random.Value!.Next(1, threshold);

            for (int i = dropList.Groups.Count - 1; i >= 0; i--)
            {
                var group = dropList.Groups[i];
                threshold -= group.Rate;
                if (threshold <= randomValue)
                    return group;
            }

            return dropList.Groups[dropList.Groups.Count - 1];
        }

        private static InitItemDataSet CreateItemDataSet(ItemIdSet idSet)
        {
            return new InitItemDataSet(
                (int)idSet.Id,
                GenerateRandomCode(),
                random.Value!.Next(1, idSet.StackMax + 1)
            );
        }

        public static List<InitItemDataSet> GetRandomItemIDsFromData(int dropListId, bool isShopData = false)
        {
            var data = GetData(isShopData);
            var result = new List<InitItemDataSet>();

            if (!data.DropLists.TryGetValue(dropListId.ToString(), out var dropList))
                return result;

            // Process fixed groups
            foreach (var fixedGroup in dropList.FixedGroups)
            {
                foreach (var idSet in fixedGroup.IdSets)
                {
                    if (result.Count >= dropList.MaxDropCount)
                        return result;
                    result.Add(CreateItemDataSet(idSet));
                }
            }

            // Process random drops
            if (result.Count < dropList.MinDropCount && dropList.Groups.Count > 0)
            {
                int remaining = dropList.MaxDropCount - result.Count;
                int minNeeded = Math.Max(1, dropList.MinDropCount - result.Count);
                int count = random.Value!.Next(minNeeded, remaining + 1);

                for (int i = 0; i < count; i++)
                {
                    var group = SelectGroup(dropList);
                    if (group.IdSets.Count > 0)
                    {
                        var randomItem = group.IdSets[random.Value!.Next(group.IdSets.Count)];
                        result.Add(CreateItemDataSet(randomItem));
                    }
                }
            }

            return result;
        }

        public static List<InitItemDataSet> ChestDrop(int dropListId, bool isShopData = false)
        {
            var data = GetData(isShopData);
            var result = new List<InitItemDataSet>();

            if (dropListId <= 0 || !data.DropLists.TryGetValue(dropListId.ToString(), out var dropList))
                return result;

            float dropRate = dropList.DropRate > 0 ? (float)dropList.DropRate : 0.3f;

            if (random.Value!.NextDouble() <= dropRate)
            {
                foreach (var group in dropList.FixedGroups)
                {
                    if (data.Groups.TryGetValue(group.CommonGroupID.ToString(), out var groupData))
                    {
                        foreach (var idSet in groupData.IdSets)
                        {
                            result.Add(CreateItemDataSet(idSet));
                        }
                    }
                }

                if (result.Count < dropList.MinDropCount && dropList.Groups.Count > 0)
                {
                    int remaining = dropList.MaxDropCount - result.Count;
                    int minNeeded = Math.Max(1, dropList.MinDropCount - result.Count);
                    int count = random.Value!.Next(minNeeded, remaining + 1);

                    for (int i = 0; i < count; i++)
                    {
                        var group = SelectGroup(dropList);
                        if (group.IdSets.Count > 0)
                        {
                            var randomItem = group.IdSets[random.Value!.Next(group.IdSets.Count)];
                            result.Add(CreateItemDataSet(randomItem));
                        }
                    }
                }
            }

            return result;
        }
    }
}