namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public sealed class ItemDef
    {
        public ItemDef(ushort id, string name, int value)
        {
            Id = id;
            Name = name;
            Value = value;
        }

        public ushort Id { get; }
        public string Name { get; }

        /// <summary>Coins earned when sold.</summary>
        public int Value { get; }
    }
}
