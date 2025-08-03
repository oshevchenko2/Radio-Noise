using UnityEngine;
using System;
using FishNet.Object;

namespace Movement
{
    [Serializable]
    public class PlayerMovementStateMachine : NetworkBehaviour
    {
        public PlayerMovementState CurrentState { get; private set; }
        [SerializeField] private PlayerMovementStack _stack;
        private PlayerMovementState _previousState;

        private void Update()
        {
            if (CurrentState == null)
            {
                return;
            }
            CurrentState.Update();
        }
        
        public void Begin(PlayerMovementState state)
        {
            _stack = new PlayerMovementStack();
            _stack.Push(state);
            CurrentState = state;
            CurrentState.Enter();
        }

        public void SetState(PlayerMovementState state)
        {
            CurrentState?.Exit();

            CurrentState = state;
            _stack.Push(state);
            CurrentState.Enter();
        }

        public void Dispose()
        {
            if (_stack.Count() == 0)
                return;

            CurrentState.Exit();
            CurrentState = null;
            _stack.Pop();

            if (_stack.Count() == 0)
                return;

            CurrentState = _stack.Peek();
            CurrentState.Enter();
        }
    }
}
