using System;
using UnityEngine.ProBuilder;

namespace Movement
{
    [Serializable]
    public class PlayerMovementState
    {
        protected PlayerMovementStateMachine stateMachine;

        public PlayerMovementState(PlayerMovementStateMachine stateMachine) => this.stateMachine = stateMachine;

        protected bool IsOwner => stateMachine.IsOwner;

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }

    }
}
