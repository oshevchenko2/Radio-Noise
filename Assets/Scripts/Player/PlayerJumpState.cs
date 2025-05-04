using UnityEngine;
using Movement;
using UnityEditor.Experimental.GraphView;

namespace Player
{
    public class PlayerJumpState : PlayerMovementState
    {
        private CharacterController _controller;
        private float _jumpForce = 5f;
        private readonly float _gravity = 9.81f;
        private Vector3 velocity;

        public PlayerJumpState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_controller.velocity * Time.deltaTime);
        }

        public override void Update()
        {
            if(Physics.Raycast(origin:stateMachine.transform.position, direction:Vector3.down, maxDistance: stateMachine.transform.localScale.y + 0.2f))
            {
                stateMachine.Begin(new PlayerGroundedState(stateMachine));
            }
        }
    }
}
