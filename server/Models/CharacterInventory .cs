namespace Ninelives_Offline.Models
{
    public class Slot
    {
        public int SlotNumber { get; set; }
        public int ItemID { get; set; }
        public int RandomCode { get; set; }
        public int ItemCount { get; set; }
        public int Lfc { get; set; }  // Last frame Count ?!
    }

    public class CharacterInventory
    {
        public string CsKey { get; set; }
        public string DockType { get; set; }
        public string Slots { get; set; }
        public int Lfc { get; set; }
    }
}
