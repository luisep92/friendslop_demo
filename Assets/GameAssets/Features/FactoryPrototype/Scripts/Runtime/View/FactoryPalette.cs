using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>Greybox colors. Replace with real assets later.</summary>
    public static class FactoryPalette
    {
        public static readonly Color GhostValid = new Color(0.3f, 0.9f, 0.4f, 0.45f);
        public static readonly Color GhostInvalid = new Color(0.95f, 0.25f, 0.2f, 0.45f);
        public static readonly Color GhostDismantle = new Color(1f, 0.15f, 0.1f, 0.55f);
        public static readonly Color GhostDismantleHover = new Color(1f, 0.55f, 0.45f, 0.3f);
        public static readonly Color GhostPending = new Color(0.35f, 0.65f, 1f, 0.4f);
        public static readonly Color InputPort = new Color(0.2f, 0.8f, 0.3f);
        public static readonly Color OutputPort = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Arrow = new Color(0.95f, 0.95f, 0.95f);

        public static Color Item(ushort itemId)
        {
            switch (itemId)
            {
                case PrototypeContent.Herb: return new Color(0.25f, 0.75f, 0.25f);
                case PrototypeContent.Crystal: return new Color(0.3f, 0.85f, 1f);
                case PrototypeContent.Powder: return new Color(0.85f, 0.75f, 0.5f);
                case PrototypeContent.Potion: return new Color(0.9f, 0.2f, 0.85f);
                default: return Color.white;
            }
        }

        public static Color Building(BuildingDef def)
        {
            switch (def.Kind)
            {
                case BuildingKind.Belt: return new Color(0.22f, 0.22f, 0.25f);
                case BuildingKind.Source: return Color.Lerp(Item(def.SourceItem), Color.black, 0.35f);
                case BuildingKind.Sink: return new Color(0.95f, 0.75f, 0.15f);
                default: return def.Id == PrototypeContent.Mixer ? new Color(0.45f, 0.35f, 0.7f) : new Color(0.55f, 0.45f, 0.35f);
            }
        }
    }
}
