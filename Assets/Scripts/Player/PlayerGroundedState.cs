using UnityEngine;
using System;
using Movement;
using System.Collections.Generic;

namespace Player
{
    public class PlayerGroundedState : PlayerMovementState
    {
        private readonly float _currentMovementSpeed = 15f;
        private float _verticalVelocity = 0.0f;
        private readonly float _gravity = 9.81f;
        private CharacterController _controller;
        public PlayerGroundedState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

        public override void Enter() => _controller = stateMachine.GetComponent<CharacterController>();

        public override void Update()
        {        
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = (stateMachine.transform.right * horizontal + stateMachine.transform.forward * vertical).normalized;

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
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                stateMachine.SetState(new PlayerJumpState(stateMachine, finalMove * 0.2f));
            }
            
        }
    }
}
