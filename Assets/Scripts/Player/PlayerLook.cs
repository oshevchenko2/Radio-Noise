// This project & code is licensed under the MIT License. See the ./LICENSE file for details.using UnityEngine;
using Movement;

namespace Player
{
    public class PlayerLook : MonoBehaviour
    {
        public float MouseSensitivity = 2f;
        public float MaxLookAngle = 85.0f;
        private float _rotationX = 0.0f;

        private Transform _cameraTransform;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            
            _cameraTransform = Camera.main.transform;
        }
        private void Update()
        {
            float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity;
            this.transform.Rotate(0, mouseX, 0);
            float mouseY = -Input.GetAxis("Mouse Y") * MouseSensitivity;

            _rotationX = Mathf.Clamp(_rotationX + mouseY, -MaxLookAngle, MaxLookAngle);
            _cameraTransform.localEulerAngles = new Vector3(_rotationX, 0, 0);
        }
    }
}