using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Owner-only first-person movement and look. Position replicates through a client-authoritative
    /// NetworkTransform. The camera is created at runtime for the owner and replaces the scene's overview camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintMultiplier = 1.6f;
        [SerializeField] private float _jumpHeight = 1.1f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _lookSensitivity = 0.1f;
        [SerializeField] private float _eyeHeight = 1.6f;
        [SerializeField] private Renderer _body;

        private CharacterController _controller;
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _sprint;
        private Camera _overviewCamera;
        private float _pitch;
        private float _verticalVelocity;

        public static FirstPersonController Local { get; private set; }
        public Camera Camera { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _controller.enabled = IsOwner;
            if (!IsOwner)
                return;

            Local = this;
            _move = Enable("Player/Move");
            _look = Enable("Player/Look");
            _jump = Enable("Player/Jump");
            _sprint = Enable("Player/Sprint");

            _overviewCamera = Camera.main;
            if (_overviewCamera != null)
                _overviewCamera.gameObject.SetActive(false);

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, _eyeHeight, 0f);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            // Own body only blocks the view.
            if (_body != null)
                _body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            SetCursorLocked(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local != this)
                return;

            Local = null;
            if (_overviewCamera != null)
                _overviewCamera.gameObject.SetActive(true);
            SetCursorLocked(false);
        }

        /// <summary>Server asks the owner to move: position is client-authoritative (save load).</summary>
        [TargetRpc]
        public void TargetTeleport(NetworkConnection connection, Vector3 position, float yaw)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = true;
            _verticalVelocity = 0f;
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (!IsOwner || Camera == null)
                return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = _look.ReadValue<Vector2>() * _lookSensitivity;
                transform.Rotate(0f, look.x, 0f);
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
                Camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector2 input = _move.ReadValue<Vector2>();
            float speed = _moveSpeed * (_sprint.IsPressed() ? _sprintMultiplier : 1f);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (_jump.WasPressedThisFrame())
                    _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
            _verticalVelocity += _gravity * Time.deltaTime;

            _controller.Move((horizontal + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private static InputAction Enable(string path)
        {
            InputAction action = InputSystem.actions.FindAction(path, true);
            action.Enable();
            return action;
        }
    }
}
