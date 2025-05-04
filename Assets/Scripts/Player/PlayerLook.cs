using UnityEngine;
using Movement;

namespace Player
{
    public class PlayerLook : PlayerMovementState
    {
        public float MouseSensitivity = 2f;
        public float MaxLookAngle = 85.0f;
        private float _rotationX = 0.0f;

        private Transform _cameraTransform;

        public PlayerLook(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            Cursor.lockState = CursorLockMode.Locked;
            
            _cameraTransform = Camera.main.transform;
        }
        public override void Update()
        {
            float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity;
            stateMachine.transform.Rotate(0, mouseX, 0);
            float mouseY = -Input.GetAxis("Mouse Y") * MouseSensitivity;

            _rotationX = Mathf.Clamp(_rotationX + mouseY, -MaxLookAngle, MaxLookAngle);
            _cameraTransform.localEulerAngles = new Vector3(_rotationX, 0, 0);
        }
    }
}