using Newtonsoft.Json;
using Ninelives_Offline.Models;
using Ninelives_Offline.Utilities;


namespace Ninelives_Offline.Data
{
    public class RecipeData
    {
        private static Dictionary<string, List<Recipe>> _vendorData;

        static RecipeData()
        {
            // Initialize JSON data (hardcoded or loaded from a file)
            var vendorDataJson = Helpers.GetEmbeddedResource("Ninelives_Offline.Data.VendorData.json");
            // Deserialize once and store in memory
            _vendorData = JsonConvert.DeserializeObject<Dictionary<string, List<Recipe>>>(vendorDataJson);
        }

        public static List<Recipe> GetRecipesForVendor(string vendorID, int keyItemId)
        {
            if (_vendorData.ContainsKey(vendorID))
            {
                return _vendorData[vendorID].Where(r => r.KeyItemID == keyItemId).ToList();
            }
            return new List<Recipe>();
        }
    }
}
