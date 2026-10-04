using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public readonly struct ItemAmount
    {
        public ItemAmount(ushort item, int count)
        {
            Item = item;
            Count = count;
        }

        public ushort Item { get; }
        public int Count { get; }
    }

    public sealed class RecipeDef
    {
        private readonly ItemAmount[] _inputs;
        private readonly ItemAmount[] _outputs;

        public RecipeDef(ItemAmount[] inputs, ItemAmount[] outputs, int durationTicks)
        {
            _inputs = inputs;
            _outputs = outputs;
            DurationTicks = durationTicks;
        }

        public IReadOnlyList<ItemAmount> Inputs => _inputs;
        public IReadOnlyList<ItemAmount> Outputs => _outputs;
        public int DurationTicks { get; }

        /// <summary>Index of the input slot for the item, or -1.</summary>
        public int IndexOfInput(ushort item)
        {
            for (int i = 0; i < _inputs.Length; i++)
            {
                if (_inputs[i].Item == item)
                    return i;
            }
            return -1;
        }
    }
}
