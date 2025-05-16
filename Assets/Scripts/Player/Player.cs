// This project & code is licensed under the MIT License. See the ./LICENSE file for details.
using System;
using Movement;
using UnityEngine;
using UnityEditor;

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
