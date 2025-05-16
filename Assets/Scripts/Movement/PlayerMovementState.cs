// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Movement
{
    [Serializable]
    public class PlayerMovementState
    {
        protected PlayerMovementStateMachine stateMachine;

        public PlayerMovementState(PlayerMovementStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }

    }
}
