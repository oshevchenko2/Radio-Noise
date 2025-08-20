using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerLook : NetworkBehaviour
    {
        public float MouseSensitivity = 2f;
        public float MaxLookAngle = 85.0f;
        public Transform cameraHolder;

        private float _rotationX = 0.0f;

        private InputAction _lookAction;

        private void Awake()
        {
            _lookAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
            _lookAction.Enable();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner || !Owner.IsLocalClient)
            {
                cameraHolder.gameObject.SetActive(false);
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!IsOwner) return;

            Vector2 delta = _lookAction.ReadValue<Vector2>();

            float scaleFactor = 0.1f;
            Vector2 adjustedDelta = MouseSensitivity * scaleFactor * delta;

            transform.Rotate(Vector3.up * adjustedDelta.x);

            _rotationX = Mathf.Clamp(_rotationX - adjustedDelta.y, -MaxLookAngle, MaxLookAngle);
            cameraHolder.localEulerAngles = new Vector3(_rotationX, 0f, 0f);
        }

        private void OnDestroy()
        {
            _lookAction.Disable();
        }
    }
}
