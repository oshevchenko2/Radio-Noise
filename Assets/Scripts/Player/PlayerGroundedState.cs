using UnityEngine;
using UnityEngine.InputSystem;
using Movement;

namespace Player
{
    public class PlayerGroundedState : PlayerMovementState
    {
        private readonly float _currentMovementSpeed = 10f;
        private float _verticalVelocity = 0.0f;
        private readonly float _gravity = 9.81f;
        private CharacterController _controller;

        private readonly InputAction _moveAction;
        private readonly InputAction _jumpAction;

        public PlayerGroundedState(PlayerMovementStateMachine stateMachine) : base(stateMachine)
        {
            _moveAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _jumpAction = new InputAction(type: InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");

            _moveAction.Enable();
            _jumpAction.Enable();
        }

        public override void Enter()
        {
            _controller = stateMachine.GetComponent<CharacterController>();
        }

        public override void Update()
        {
            if (!IsOwner) return;
            
            Vector2 input = _moveAction.ReadValue<Vector2>();
            Vector3 moveDirection = (stateMachine.transform.right * input.x + stateMachine.transform.forward * input.y).normalized;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * Time.deltaTime;
            }

            Vector3 finalMove = moveDirection * _currentMovementSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(finalMove * Time.deltaTime);

            if (_jumpAction.triggered)
            {
                stateMachine.SetState(new PlayerJumpState(stateMachine, finalMove * 0.1f));
            }
        }

        public override void Exit()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
        }
    }
}
