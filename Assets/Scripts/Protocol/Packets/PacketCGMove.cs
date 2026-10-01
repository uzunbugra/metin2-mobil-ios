using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Move function codes (EMoveFuncType, server packet.h:570-583).
    /// FUNC_SKILL carries a skill motion in the low 7 bits
    /// (MASK_SKILL_MOTION = 0x7F, `input_main.cpp:1615-1616`).
    /// </summary>
    public static class MoveFunc
    {
        public const byte Wait = 0;
        public const byte Move = 1;
        public const byte Attack = 2;
        public const byte Combo = 3;
        public const byte MobSkill = 4;
        public const byte MaxNum = 5;
        public const byte Skill = 0x80;
        public const byte SkillMotionMask = 0x7F;
    }

    /// <summary>
    /// Outgoing movement intent (command_move, 16 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:586-595`, handled `input_main.cpp:1514-1688`
    /// (server-authoritative: teleport/speed/combo checks, FUNC_MOVE → Goto,
    /// rotation = bRot*5 degrees, rebroadcast GC_MOVE to viewers);
    /// Client `UserInterface/Packet.h:695-704`, sent
    /// `PhaseGame.cpp:1107-1145` (`SendCharacterStatePacket`: bRot = degrees/5,
    /// coords in cm, dwTime = server-synced ms).
    /// Wire layout (total 16 = 1+3+8+4):
    /// [0]     BYTE  bHeader (7 = 0x07)
    /// [1]     BYTE  bFunc
    /// [2]     BYTE  bArg
    /// [3]     BYTE  bRot (degrees / 5)
    /// [4..7]  long  lX (cm, LE)
    /// [8..11] long  lY (cm, LE)
    /// [12..15] DWORD dwTime (server ms, LE)
    /// </summary>
    public struct PacketCGMove : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_MOVE;
        public const int PacketSize = 1 + 3 + 8 + 4; // 16 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Func { get; set; }
        public byte Arg { get; set; }
        public byte Rot { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public uint Time { get; set; }

        public PacketCGMove(byte func, byte arg, byte rot, int x, int y, uint time)
        {
            Func = func;
            Arg = arg;
            Rot = rot;
            X = x;
            Y = y;
            Time = time;
        }

        public override string ToString()
        {
            return $"PacketCGMove(Func={Func}, Rot={Rot}, Pos=({X},{Y}))";
        }
    }
}
