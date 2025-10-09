namespace VladiCore.Domain.Entities
{
    public class Psu
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Wattage { get; set; }
        public string FormFactor { get; set; }
    }
}
