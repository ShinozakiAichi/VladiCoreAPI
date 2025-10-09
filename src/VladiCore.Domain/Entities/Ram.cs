namespace VladiCore.Domain.Entities
{
    public class Ram
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int Freq { get; set; }
        public int CapacityPerStick { get; set; }
        public int Sticks { get; set; }
        public int PerfScore { get; set; }
    }
}
