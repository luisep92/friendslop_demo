namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Content for the minimal line prototype. Move to ScriptableObjects once the idea is validated.
    /// </summary>
    public static class PrototypeContent
    {
        // Items.
        public const ushort Herb = 1;
        public const ushort Crystal = 2;
        public const ushort Powder = 3;
        public const ushort Potion = 4;

        // Buildings. Declaration order in Create() = build slot order.
        public const ushort Belt = 1;
        public const ushort HerbSource = 2;
        public const ushort CrystalSource = 3;
        public const ushort Grinder = 4;
        public const ushort Mixer = 5;
        public const ushort SellPoint = 6;

        public static ContentDb Create()
        {
            var items = new[]
            {
                new ItemDef(Herb, "Herb", 1),
                new ItemDef(Crystal, "Crystal", 2),
                new ItemDef(Powder, "Powder", 4),
                new ItemDef(Potion, "Potion", 15)
            };

            var grind = new RecipeDef(
                new[] { new ItemAmount(Herb, 1) },
                new[] { new ItemAmount(Powder, 1) },
                30);

            var mix = new RecipeDef(
                new[] { new ItemAmount(Powder, 1), new ItemAmount(Crystal, 1) },
                new[] { new ItemAmount(Potion, 1) },
                60);

            var buildings = new[]
            {
                BuildingDef.Belt(Belt, "Belt"),
                BuildingDef.Source(HerbSource, "Herb Source", Herb, 20),
                BuildingDef.Source(CrystalSource, "Crystal Source", Crystal, 40),
                BuildingDef.Crafter(Grinder, "Grinder", 1, 1, new[]
                {
                    PortDef.In(0, 0, Dir.South),
                    PortDef.Out(0, 0, Dir.North)
                }, grind),
                BuildingDef.Crafter(Mixer, "Mixer", 2, 1, new[]
                {
                    PortDef.In(0, 0, Dir.South),
                    PortDef.In(1, 0, Dir.South),
                    PortDef.Out(0, 0, Dir.North)
                }, mix),
                BuildingDef.Sink(SellPoint, "Sell Point")
            };

            return new ContentDb(items, buildings);
        }
    }
}
