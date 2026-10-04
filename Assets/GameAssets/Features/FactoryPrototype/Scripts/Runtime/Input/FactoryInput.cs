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
            Place = Create("Place", "<Mouse>/leftButton");
            Remove = Create("Remove", "<Mouse>/rightButton");
            Rotate = Create("Rotate", "<Keyboard>/r");
            ToggleCursor = Create("ToggleCursor", "<Keyboard>/tab");
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = Create($"Slot{i + 1}", $"<Keyboard>/{i + 1}");
        }

        public InputAction Place { get; }
        public InputAction Remove { get; }
        public InputAction Rotate { get; }
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
            Place.Dispose();
            Remove.Dispose();
            Rotate.Dispose();
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
