using UnityEngine;

public class CameraMovement : MonoBehaviour {
    public float moveSpeed = 5.0f;
    
    [Range(1, 500f)]
    public float mouseSensitivity = 100.0f;
    private float xRotation = 0f;
    
    void Start() {
        
        // Cursor.lockState = CursorLockMode.Locked; 
    }
    
    void Update() {
        
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = (transform.right * horizontalInput + transform.forward * verticalInput).normalized;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
        
        if (Input.GetMouseButton(1)) {
            
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            transform.Rotate(Vector3.up * mouseX);
            transform.localRotation = Quaternion.Euler(xRotation, transform.localEulerAngles.y, 0f);
        }
    }
}