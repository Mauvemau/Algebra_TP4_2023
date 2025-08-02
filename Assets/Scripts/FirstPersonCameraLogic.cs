using UnityEngine;

public class FirstPersonCameraLogic : MonoBehaviour
{
    public float moveSpeed = 5.0f; // Speed at which the camera moves
    public float sensitivity = 2.0f; // Mouse sensitivity
    private Vector3 _lastMousePosition; // Store the last mouse position

    private void LateUpdate()
    {
        if (Input.GetMouseButtonDown(1))
        {
            _lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButton(1))
        {
            var mouseDelta = (Input.mousePosition - _lastMousePosition) * sensitivity;
            transform.Rotate(-mouseDelta.y, mouseDelta.x, 0);
            _lastMousePosition = Input.mousePosition;

            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0);
        }

        var horizontalInput = Input.GetAxis("Horizontal");
        var verticalInput = Input.GetAxis("Vertical");

        var moveDirection = new Vector3(horizontalInput, 0, verticalInput) * (moveSpeed * Time.fixedDeltaTime);
        transform.Translate(moveDirection);
    }
}
