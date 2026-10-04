using System;
using UnityEngine.InputSystem;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Build actions created in code, so the shared project-wide actions asset stays untouched.
    /// Owned and disposed by the local player's BuildTool.
    /// </summary>
    public sealed class FactoryInput : IDisposable
    {
        public const int SlotCount = 6;

        private readonly InputAction[] _slots = new InputAction[SlotCount];

        public FactoryInput()
        {
            Primary = Create("Primary", "<Mouse>/leftButton");
            Secondary = Create("Secondary", "<Mouse>/rightButton");
            Sample = Create("Sample", "<Mouse>/middleButton");
            Rotate = Create("Rotate", "<Keyboard>/r");
            Dismantle = Create("Dismantle", "<Keyboard>/f");
            Modifier = Create("Modifier", "<Keyboard>/ctrl");
            ToggleCursor = Create("ToggleCursor", "<Keyboard>/tab");
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = Create($"Slot{i + 1}", $"<Keyboard>/{i + 1}");
        }

        /// <summary>Place / confirm. Dismantle: click marks, hold dismantles.</summary>
        public InputAction Primary { get; }

        /// <summary>Cancel the current belt run. Dismantle: clear the selection.</summary>
        public InputAction Secondary { get; }

        /// <summary>Dismantle: held, adds whatever is aimed at to the selection.</summary>
        public InputAction Modifier { get; }

        /// <summary>Copy type and rotation of the targeted building.</summary>
        public InputAction Sample { get; }

        /// <summary>Rotate, or flip the corner of a belt run.</summary>
        public InputAction Rotate { get; }

        /// <summary>Toggle dismantle mode.</summary>
        public InputAction Dismantle { get; }

        public InputAction ToggleCursor { get; }

        /// <summary>Index of the slot key pressed this frame, or -1.</summary>
        public int PressedSlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i].WasPressedThisFrame())
                    return i;
            }
            return -1;
        }

        public void Dispose()
        {
            Primary.Dispose();
            Secondary.Dispose();
            Sample.Dispose();
            Rotate.Dispose();
            Dismantle.Dispose();
            Modifier.Dispose();
            ToggleCursor.Dispose();
            foreach (InputAction slot in _slots)
                slot.Dispose();
        }

        private static InputAction Create(string name, string binding)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.Enable();
            return action;
        }
    }
}
