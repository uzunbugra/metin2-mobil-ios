using Metin2.Tools.ProtoConverter;

namespace Metin2.Tools.ProtoConverter
{
    /// <summary>
    /// SP10-2 — proto converter CLI (JSON output; the JSON embeds
    /// copyrighted name strings, keep it local — ADR-0003).
    ///
    /// Usage:
    ///   metin2-protoconverter mob    <mob_proto>  <out.json>
    ///   metin2-protoconverter item   <item_proto> <out.json>
    ///                                [--item-list item_list.txt]
    ///                                [--itemdesc itemdesc.txt]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                PrintUsage();
                return 2;
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "mob":
                        MobProto.Convert(args[1], args[2]);
                        return 0;
                    case "item":
                    {
                        string? itemList = FindOption(args, "--item-list");
                        string? itemDesc = FindOption(args, "--itemdesc");
                        ItemProto.Convert(args[1], args[2], itemList, itemDesc);
                        return 0;
                    }

                    default:
                        PrintUsage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }

        private static string? FindOption(string[] args, string name)
        {
            for (int i = 3; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  metin2-protoconverter mob  <mob_proto> <out.json>\n" +
                "  metin2-protoconverter item <item_proto> <out.json> [--item-list item_list.txt] [--itemdesc itemdesc.txt]");
        }
    }
}
