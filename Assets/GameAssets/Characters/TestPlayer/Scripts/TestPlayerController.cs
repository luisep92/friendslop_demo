using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Friendslop.Characters.TestPlayer
{
    /// <summary>
    /// Minimal owner-driven movement to validate sync. Position replicates through a client-authoritative NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TestPlayerController : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private Renderer _renderer;

        private static readonly Color[] OwnerColors = { Color.red, Color.cyan, Color.yellow, Color.green, Color.magenta };

        private CharacterController _controller;
        private InputAction _move;
        private InputAction _jump;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _controller.enabled = IsOwner;

            if (_renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", OwnerColors[Mathf.Abs(OwnerId) % OwnerColors.Length]);
                _renderer.SetPropertyBlock(block);
            }

            if (IsOwner)
            {
                _move = InputSystem.actions.FindAction("Player/Move", true);
                _jump = InputSystem.actions.FindAction("Player/Jump", true);
            }
        }

        private void Update()
        {
            if (!IsOwner || _move == null)
                return;

            Vector2 input = _move.ReadValue<Vector2>();
            Vector3 horizontal = new Vector3(input.x, 0f, input.y) * _moveSpeed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (_jump.WasPressedThisFrame())
                    _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
            _verticalVelocity += _gravity * Time.deltaTime;

            _controller.Move((horizontal + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }
    }
}
