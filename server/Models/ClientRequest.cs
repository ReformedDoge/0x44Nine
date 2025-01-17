using Newtonsoft.Json;

namespace Ninelives_Offline.Models
{
    public class ClientRequest
    {
        [JsonProperty("ver_major")]
        public string VerMajor { get; set; }

        [JsonProperty("ver_minor")]
        public string VerMinor { get; set; }

        [JsonProperty("ver_rivision")]
        public string VerRevision { get; set; }

        [JsonProperty("id")]
        public string Email { get; set; }

        [JsonProperty("sessionKey")]
        public string SessionKey { get; set; }
    }

    public class DropRequest
    {
        [JsonProperty("dropID")]
        public string DropID { get; set; }
    }
    public class SaveCharacterRequest
    {
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
        public string LastZoneID { get; set; }
        public string Exp { get; set; }
        public string Gold { get; set; }
        public string KillCount { get; set; }
        public string DeadCount { get; set; }
        public string PlayTime { get; set; }
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRevision { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Race { get; set; }
        public string Job { get; set; }
        public string Hair { get; set; }
        public string HairColor { get; set; }
        public string FacialFileHead { get; set; }
        public string Facial { get; set; }

        [JsonProperty("portrait")]
        public string Portrait { get; set; }
        public string HireUseDefaultColor { get; set; }
        public string BagSlotCount { get; set; }
        public string BankPageCount { get; set; }
        public string SkillTreePayExp { get; set; }
        public string BitCount { get; set; }
        public string Bits1 { get; set; }
        public string Bits2 { get; set; }
        public string Bits3 { get; set; }
        public string Bits4 { get; set; }
        [JsonProperty("flagCode1")]
        public string FlagCode1 { get; set; }
        [JsonProperty("flagCode2")]
        public string FlagCode2 { get; set; }
        [JsonProperty("flagCode3")]
        public string FlagCode3 { get; set; }
        [JsonProperty("flagCode4")]
        public string FlagCode4 { get; set; }
        [JsonProperty("flagCode5")]
        public string FlagCode5 { get; set; }
        [JsonProperty("flagCode6")]
        public string FlagCode6 { get; set; }
        [JsonProperty("flagCode7")]
        public string FlagCode7 { get; set; }
        [JsonProperty("flagCode8")]
        public string FlagCode8 { get; set; }
    }

    public class LoadCharacterResponse
    {
        public string Name { get; set; }
        public int Race { get; set; }
        public int Job { get; set; }
        public int Hair { get; set; }
        public int HairColor { get; set; }
        public string FacialFileHead { get; set; }
        public int Facial { get; set; }
        public string SessionKey { get; set; }
        public int LastZoneID { get; set; }
        public int BagSlotCount { get; set; }
        public int BankPageCount { get; set; }
        public int Exp { get; set; }
        public int Gold { get; set; }
        public int KillCount { get; set; }
        public int DeadCount { get; set; }
        public int PlayTime { get; set; }
        public string FlagCode1 { get; set; }
        public string FlagCode2 { get; set; }
        public string FlagCode3 { get; set; }
        public string FlagCode4 { get; set; }
        public string FlagCode5 { get; set; }
        public string FlagCode6 { get; set; }
        public string FlagCode7 { get; set; }
        public string FlagCode8 { get; set; }
        public int SkillTreePayExp { get; set; }
        public int BitCount { get; set; }
        public int Bits1 { get; set; }
        public int Bits2 { get; set; }
        public int Bits3 { get; set; }
        public int Bits4 { get; set; }
    }

    public class LoadCharacterRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRevision { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string SessionKey { get; set; }
    }

    public class SaveCharacterItemsRequest
    {
        public string CsKey { get; set; }
        public string DockType { get; set; }
        public string Slots { get; set; }
        public string SessionKey { get; set; }
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string Lfc { get; set; }
        public string PageCount { get; set; }
    }
    public class Item
    {
        public int SlotNumber { get; set; }
        public int ItemID { get; set; }
        public long RandomCode { get; set; }
        public int ItemCount { get; set; }
    }
    public class LoadInboxItemsRequest
    {
        public string ver_major { get; set; }
        public string ver_minor { get; set; }
        public string ver_rivision { get; set; }
        public string id { get; set; }
        public string sessionKey { get; set; }
        public string csKey { get; set; }
    }
    public class BankUnlockByItemRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
        public string SlotId { get; set; }
        public string RandomCode { get; set; }
    }


    public class Slots
    {
        [JsonProperty("statsItems")]
        public List<Item> StatsItems { get; set; }

        [JsonProperty("bagItems")]
        public List<Item> BagItems { get; set; }

        [JsonProperty("actionItems")]
        public List<Item> ActionItems { get; set; }

        [JsonProperty("bankItems")]
        public List<Item> BankItems { get; set; }

        [JsonProperty("shareItems")]
        public List<Item> ShareItems { get; set; }
    }

    public class SaveCharacterItemsDiffRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
        public int Lfc { get; set; }
        [JsonIgnore] // Prevent direct deserialization
        public SaveCharacterItemsDiffSlots Slots { get; set; }
        [JsonProperty("slots")]
        public string SlotsString { get; set; } // Temporarily hold the serialized slots JSON
    }


    public class AlchemyMixedItemRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
        public string RecipeID { get; set; }
    }
    public class BagUnlockBySGRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
    }
    public class BankUnlockBySGRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
    }

    public class AddSharedBankRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
    }
    public class AddCharacterLimitRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
    }

    public class ResetSkillTreeRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRivision { get; set; }
        public string Id { get; set; }
        public string SessionKey { get; set; }
        public string CsKey { get; set; }
    }
    public class LoadAlchemyRecipeListRequest
    {
        public string Ver_major { get; set; }
        public string Ver_minor { get; set; }
        public string Ver_rivision { get; set; }
        public string VendorID { get; set; }
        public string KeyItemId { get; set; }
        public string RecipeID { get; set; }
    }

    public class Recipe
    {
        public int VendorID { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsHidden { get; set; }
        public int RecipeID { get; set; }
        public int KeyItemID { get; set; }
        public int KeyItemCount { get; set; }
        public int ResultItemID { get; set; }
        public int ResultItemCount { get; set; }
        public List<Material> Mats { get; set; }
    }

    public class Material
    {
        public int Id { get; set; }
        public int Count { get; set; }
    }


    public class SaveCharacterItemsDiffSlots
    {
        public List<Item> StatsItems { get; set; }
        public List<Item> BagItems { get; set; }
        public List<Item> SkillItems { get; set; }
        public List<Item> ActionItems { get; set; }
        public List<Item> LootItems { get; set; }
        public List<Item> AlchemyItems { get; set; }
        public List<Item> BankItems { get; set; }
        public List<Item> InboxItems { get; set; }
        public List<Item> ShopItems { get; set; }
        public List<Item> SelledItems { get; set; }
        public List<Item> ShareItems { get; set; }
        public List<Item> ShopSGItems { get; set; }
    }
    public class CharacterQuestRequest
    {
        public string VerMajor { get; set; }
        public string VerMinor { get; set; }
        public string VerRevision { get; set; }
        public string Id { get; set; } // User email
        public string SessionKey { get; set; }
        public string CsKey { get; set; } // Character session key

        public int QuestID { get; set; } // Quest ID for adding, completing, or removing quests
        public bool? IsRequestRemove { get; set; } // Optional: For /completeCharacterQuest to remove a completed quest
    }

    public class SaveCharacterBagBankCountRequest
    {
        [JsonProperty("ver_major")]
        public string VerMajor { get; set; }

        [JsonProperty("ver_minor")]
        public string VerMinor { get; set; }

        [JsonProperty("ver_rivision")]
        public string VerRevision { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sessionKey")]
        public string SessionKey { get; set; }

        [JsonProperty("csKey")]
        public string CsKey { get; set; }

        [JsonProperty("bagSlotCount")]
        public string BagSlotCount { get; set; }
    }
}

