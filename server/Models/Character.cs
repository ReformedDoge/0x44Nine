namespace Ninelives_Offline.Models
{
    public class Character
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public int Race { get; set; }
        public int Job { get; set; }
        public int Hair { get; set; }
        public int HairColor { get; set; }
        public string FacialFileHead { get; set; }
        public int Facial { get; set; }
        public int LastZoneID { get; set; }
        public string Portrait { get; set; }
        public bool HireUseDefaultColor { get; set; }
        public string CsKey { get; set; }
        public int Exp { get; set; } = 0;
        public int Gold { get; set; } = 0;
        public int KillCount { get; set; } = 0;
        public int DeadCount { get; set; } = 0;
        public int PlayTime { get; set; } = 0;
        public int BagSlotCount { get; set; } = 24;
        public int BankPageCount { get; set; } = 1;
        public string FlagCode1 { get; set; }
        public string FlagCode2 { get; set; }
        public string FlagCode3 { get; set; }
        public string FlagCode4 { get; set; }
        public string FlagCode5 { get; set; }
        public string FlagCode6 { get; set; }
        public string FlagCode7 { get; set; }
        public string FlagCode8 { get; set; }
        public int SkillTreePayExp { get; set; } = 0;
        public int BitCount { get; set; } = 0;
        public int Bits1 { get; set; } = 0;
        public int Bits2 { get; set; } = 0;
        public int Bits3 { get; set; } = 0;
        public int Bits4 { get; set; } = 0;
        public string SessionKey { get; set; }

        public override string ToString()
        {
            return $"Character [Name={Name}, Race={Race}, Job={Job}, Hair={Hair}, HairColor={HairColor}, " +
                   $"FacialFileHead={FacialFileHead}, Facial={Facial}, SessionKey={SessionKey}, LastZoneID={LastZoneID}, " +
                   $"BagSlotCount={BagSlotCount}, BankPageCount={BankPageCount}, Exp={Exp}, Gold={Gold}, " +
                   $"KillCount={KillCount}, DeadCount={DeadCount}, PlayTime={PlayTime}, FlagCode1={FlagCode1}, " +
                   $"FlagCode2={FlagCode2}, FlagCode3={FlagCode3}, FlagCode4={FlagCode4}, FlagCode5={FlagCode5}, " +
                   $"FlagCode6={FlagCode6}, FlagCode7={FlagCode7}, FlagCode8={FlagCode8}, SkillTreePayExp={SkillTreePayExp}, " +
                   $"BitCount={BitCount}, Bits1={Bits1}, Bits2={Bits2}, Bits3={Bits3}, Bits4={Bits4}]";
        }
    }

    public class CreateCharacterRequest
    {
        public string Id { get; set; } // Email from the client
        public string Name { get; set; }
        public int Job { get; set; }
        public int Race { get; set; }
        public int Hair { get; set; }
        public int HairColor { get; set; }
        public string FacialFileHead { get; set; }
        public int Facial { get; set; }
        public bool HireUseDefaultColor { get; set; }
        public string SessionKey { get; set; }
    }

}
