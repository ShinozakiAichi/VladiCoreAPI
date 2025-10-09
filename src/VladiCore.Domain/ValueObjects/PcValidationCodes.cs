namespace VladiCore.Domain.ValueObjects
{
    public static class PcValidationCodes
    {
        public const string CpuSocketMismatch = "CPU_SOCKET_MISMATCH";
        public const string RamTypeMismatch = "RAM_TYPE_MISMATCH";
        public const string RamFreqTooHigh = "RAM_FREQ_TOO_HIGH";
        public const string GpuTooLong = "GPU_TOO_LONG";
        public const string CoolerTooTall = "COOLER_TOO_TALL";
        public const string PsuTooWeak = "PSU_TOO_WEAK";
        public const string PsuFormFactorMismatch = "PSU_FORMFACTOR_MISMATCH";
        public const string M2SlotsExceeded = "M2_SLOTS_EXCEEDED";
        public const string PcieSlotsExceeded = "PCIE_SLOTS_EXCEEDED";
    }
}
