using System.Text;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public enum CrafterState : byte
    {
        /// <summary>Waiting for inputs.</summary>
        Idle,
        Crafting,

        /// <summary>Finished or ready, but the output buffer is full.</summary>
        OutputBlocked
    }

    /// <summary>
    /// Runs one fixed recipe. Inputs enter only through input ports, into one buffer per ingredient.
    /// Pushes one output item per tick through its output ports.
    /// </summary>
    public sealed class CrafterBuilding : Building
    {
        private readonly int[] _inputs;
        private readonly int[] _outputs;
        private bool _crafting;
        private int _progress;
        private int _crafted;

        internal CrafterBuilding(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
            : base(sim, id, def, origin, rotation)
        {
            _inputs = new int[def.Recipe.Inputs.Count];
            _outputs = new int[def.Recipe.Outputs.Count];
        }

        public RecipeDef Recipe => Def.Recipe;
        public int Progress => _progress;

        public CrafterState State
        {
            get
            {
                if (_crafting)
                    return _progress < Recipe.DurationTicks ? CrafterState.Crafting : CrafterState.OutputBlocked;
                return HasAllInputs() && !HasOutputSpace() ? CrafterState.OutputBlocked : CrafterState.Idle;
            }
        }

        public override BuildingStatus Status
        {
            get
            {
                switch (State)
                {
                    case CrafterState.Crafting: return BuildingStatus.Working;
                    case CrafterState.OutputBlocked: return BuildingStatus.Blocked;
                    default: return BuildingStatus.Waiting;
                }
            }
        }

        /// <summary>Completed crafts.</summary>
        public override int Throughput => _crafted;

        public int GetInputCount(int index) => _inputs[index];
        public int GetOutputCount(int index) => _outputs[index];

        internal override void Step(int tick)
        {
            PushOneOutput(tick);

            RecipeDef recipe = Recipe;
            if (_crafting)
            {
                if (_progress < recipe.DurationTicks)
                    _progress++;

                if (_progress >= recipe.DurationTicks && HasOutputSpace())
                {
                    for (int i = 0; i < _outputs.Length; i++)
                        _outputs[i] += recipe.Outputs[i].Count;
                    _crafting = false;
                    _progress = 0;
                    _crafted++;
                }
            }

            if (!_crafting && HasAllInputs() && HasOutputSpace())
            {
                for (int i = 0; i < _inputs.Length; i++)
                    _inputs[i] -= recipe.Inputs[i].Count;
                _crafting = true;
                _progress = 0;
            }
        }

        internal override bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick)
        {
            if (!IsInputPort(cell, entrySide))
                return false;

            int index = Recipe.IndexOfInput(item.Type);
            if (index < 0 || _inputs[index] >= SimConstants.MachineBufferCapacity)
                return false;

            _inputs[index]++;
            return true;
        }

        internal override void WriteState(SimWriter writer)
        {
            writer.WriteBool(_crafting);
            writer.WriteInt(_progress);
            writer.WriteInt(_crafted);
            for (int i = 0; i < _inputs.Length; i++)
                writer.WriteInt(_inputs[i]);
            for (int i = 0; i < _outputs.Length; i++)
                writer.WriteInt(_outputs[i]);
        }

        internal override void ReadState(SimReader reader)
        {
            _crafting = reader.ReadBool();
            _progress = reader.ReadInt();
            _crafted = reader.ReadInt();
            for (int i = 0; i < _inputs.Length; i++)
                _inputs[i] = reader.ReadInt();
            for (int i = 0; i < _outputs.Length; i++)
                _outputs[i] = reader.ReadInt();
        }

        public override string Describe()
        {
            var text = new StringBuilder();
            text.Append($"{Def.Name} #{Id} | {State}");
            if (_crafting)
                text.Append($" {_progress}/{Recipe.DurationTicks}");

            text.Append(" | in");
            for (int i = 0; i < _inputs.Length; i++)
                text.Append($" {ItemName(Recipe.Inputs[i].Item)} {_inputs[i]}/{SimConstants.MachineBufferCapacity}");

            text.Append(" | out");
            for (int i = 0; i < _outputs.Length; i++)
                text.Append($" {ItemName(Recipe.Outputs[i].Item)} {_outputs[i]}/{SimConstants.MachineBufferCapacity}");

            return text.ToString();
        }

        private bool HasAllInputs()
        {
            for (int i = 0; i < _inputs.Length; i++)
            {
                if (_inputs[i] < Recipe.Inputs[i].Count)
                    return false;
            }
            return true;
        }

        private bool HasOutputSpace()
        {
            for (int i = 0; i < _outputs.Length; i++)
            {
                if (_outputs[i] + Recipe.Outputs[i].Count > SimConstants.MachineBufferCapacity)
                    return false;
            }
            return true;
        }

        private void PushOneOutput(int tick)
        {
            for (int o = 0; o < _outputs.Length; o++)
            {
                if (_outputs[o] == 0)
                    continue;

                var item = new Item(0, Recipe.Outputs[o].Item);
                for (int p = 0; p < Ports.Count; p++)
                {
                    WorldPort port = Ports[p];
                    if (port.Type == PortType.Output && TryPush(port, item, tick))
                    {
                        _outputs[o]--;
                        return;
                    }
                }
            }
        }

        private string ItemName(ushort id) => Sim.Content.GetItem(id)?.Name ?? id.ToString();
    }
}
