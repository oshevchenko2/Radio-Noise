using System;
using Movement;

namespace Player
{
    [Serializable]
    public class Player : PlayerMovementStateMachine
    {
        private void Start()
        {
            Begin(new PlayerGroundedState(this));
        }
    }
}
