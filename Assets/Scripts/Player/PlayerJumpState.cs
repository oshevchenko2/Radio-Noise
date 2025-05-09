using UnityEngine;
using Movement;
using UnityEditor.Experimental.GraphView;

namespace Player
{
    public class PlayerJumpState : PlayerMovementState
    {
        private CharacterController _controller;
        private readonly float _currentMovementSpeed = 6f;
        private float _jumpForce = 10;
        private readonly float _gravity = 9.81f;
        private Vector3 startSpeed;
        private Vector3 velocity;

        public PlayerJumpState(PlayerMovementStateMachine stateMachine, Vector3 StartSpeed) : base(stateMachine) 
        {
            startSpeed = StartSpeed;
        }

        public override void Enter()
        {
            velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            _controller = stateMachine.GetComponent<CharacterController>();
        }

        public override void Update()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = (stateMachine.transform.right * horizontal + stateMachine.transform.forward * vertical).normalized * _currentMovementSpeed;
            velocity.y += -_gravity * Time.deltaTime;
            _controller.Move((velocity + moveDirection + startSpeed) * Time.deltaTime);
            
            if(Physics.Raycast(origin:stateMachine.transform.position, direction:Vector3.down, maxDistance: stateMachine.transform.localScale.y + 0.2f) && velocity.y <= 0)
            {
                stateMachine.Begin(new PlayerGroundedState(stateMachine));
            }
        }
    }
}
