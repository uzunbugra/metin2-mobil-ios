namespace Metin2.Protocol.Constants
{
    /// <summary>
    /// Source-verified window types (EWindows, common/length.h:445-455).
    /// Travels as the window_type BYTE of every TItemPos
    /// (common/length.h:685-752: { BYTE window_type; WORD cell; } = 3B, pack(1)).
    /// NPOS — the "no position" marker — is (Reserved, WORD_MAX).
    /// </summary>
    public static class ItemWindow
    {
        public const byte Reserved = 0;   // RESERVED_WINDOW — NPOS window; server rejects it
        public const byte Inventory = 1;  // INVENTORY
        public const byte Equipment = 2;  // EQUIPMENT
        public const byte Safebox = 3;    // SAFEBOX — server rejects moves from here
        public const byte Mall = 4;       // MALL — server rejects moves from here
        public const byte DragonSoulInventory = 5; // DRAGON_SOUL_INVENTORY
        public const byte BeltInventory = 6;       // BELT_INVENTORY
        public const byte Ground = 7;     // GROUND
    }
}
