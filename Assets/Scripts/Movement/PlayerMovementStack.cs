// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using UnityEngine;
using System.Collections.Generic;
using System;

namespace Movement
{
    [Serializable]
    public class PlayerMovementStack
    {
        [SerializeField] private List<PlayerMovementState> _stack = new();

        public PlayerMovementState Peek()
        {
            if (_stack.Count == 0)
                return null;

            return _stack[^1];
        }

        public PlayerMovementState Pop()
        {
            PlayerMovementState lastState = Peek();
            _stack.RemoveAt(index:_stack.Count - 1);
            return lastState;
        }

        public void Push(PlayerMovementState state) => _stack.Add(state);

        public int Count() => _stack.Count;

        public List<PlayerMovementState> GetStack() => _stack;
    }
}
