using UnityEngine;
using UnityEngine.InputSystem;
using Movement;

namespace Player
{
    public class PlayerJumpState : PlayerMovementState
    {
        private CharacterController _controller;
        private readonly float _currentMovementSpeed = 3f;
        private float _jumpForce = 4;
        private readonly float _gravity = 9.81f;
        private Vector3 startSpeed;
        private Vector3 velocity;

        private readonly InputAction _moveAction;

        public PlayerJumpState(PlayerMovementStateMachine stateMachine, Vector3 StartSpeed) : base(stateMachine) 
        {
            startSpeed = StartSpeed;

            _moveAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.Enable();
        }

        public override void Enter()
        {
            velocity.y = Mathf.Sqrt(_jumpForce * -2f * -_gravity);
            _controller = stateMachine.GetComponent<CharacterController>();
        }

        public override void Update()
        {
            if (!IsOwner) return;
            
            Vector2 input = _moveAction.ReadValue<Vector2>();

            Vector3 moveDirection = 
                (stateMachine.transform.right * input.x + stateMachine.transform.forward * input.y).normalized 
                * _currentMovementSpeed;

            velocity.y += -_gravity * Time.deltaTime;

            _controller.Move((velocity + moveDirection + startSpeed) * Time.deltaTime);

            if (Physics.Raycast(
                origin: stateMachine.transform.position, 
                direction: Vector3.down, 
                maxDistance: stateMachine.transform.localScale.y + 0.1f) 
                && velocity.y <= 0)
            {
                stateMachine.Begin(new PlayerGroundedState(stateMachine));
            }
        }

        public override void Exit()
        {
            _moveAction.Disable();
        }
    }
}
