namespace VladiCore.Domain.Entities
{
    public class Motherboard
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Socket { get; set; }
        public string RamType { get; set; }
        public int RamMaxFreq { get; set; }
        public int M2Slots { get; set; }
        public int PcieSlots { get; set; }
        public string FormFactor { get; set; }
    }
}
