using System;
using Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [Serializable]
    public class PlayerDuckState : PlayerMovementState
    {
        private CharacterController _controller;

        private readonly float _currentMovementSpeed = 3f;
        private readonly float _gravity = 9.81f;

        private Vector3 startSpeed;
        private Vector3 velocity;

        private readonly InputAction _moveAction;
        private readonly InputAction _duckAction;

        private bool _isDucking = false;

        public PlayerDuckState(PlayerMovementStateMachine stateMachine) : base(stateMachine)
        {
            _duckAction = new InputAction(type: InputActionType.Button);
            _moveAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick");

            _duckAction.AddBinding("<Keyboard>/leftCtrl");
            _duckAction.AddBinding("<Gamepad>/buttonEast");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _duckAction.performed += OnDuckPerformed;
            _duckAction.canceled += OnDuckCanceled;

            _duckAction.Enable();
            _moveAction.Enable();
        }

        private void OnDuckPerformed(InputAction.CallbackContext ctx) => StartDuck();
        private void OnDuckCanceled(InputAction.CallbackContext ctx) => StopDuck();

        private void StartDuck()
        {
            stateMachine.transform.localScale = new Vector3(1, 0.5f, 1);

            _isDucking = true;
        }

        private void StopDuck()
        {
            if (_controller != null)
                stateMachine.transform.localScale = new Vector3(1, 1, 1);

            _isDucking = false;

            stateMachine.Begin(new PlayerGroundedState(stateMachine));
        }

        public override void Enter()
        {
            if (_controller == null) _controller = stateMachine.GetComponent<CharacterController>();
            if (_duckAction.IsPressed()) StartDuck();
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
        }

        public override void Exit()
        {
            _duckAction.performed -= OnDuckPerformed;
            _duckAction.canceled -= OnDuckCanceled;

            _duckAction.Disable();
            _moveAction.Disable();
        }
    }
}