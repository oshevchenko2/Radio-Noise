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
