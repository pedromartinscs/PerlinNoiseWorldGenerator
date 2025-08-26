using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public Transform pivot; // CameraPivot (rotated for orbit)
    public Transform cam;   // MainCamera (child of pivot)
    
    public float panSpeed = 0.5f;
    public float zoomSpeed = 10f;
    public float zoomMin = 10f;
    public float zoomMax = 80f;

	[SerializeField] float orbitSpeed = 3f;          // deg/sec
	[SerializeField, Range(-30f, 5f)] float minPitch = -10f;     // up look limit
	[SerializeField, Range(10f, 45f)]  float maxPitch = 40f;     // down look limit
	[SerializeField, Range(-10f, 20f)] float startPitch = 0f;  // initial camera pitch
	[SerializeField] bool invertOrbitY = false;

	private float currentPitch = 0f;
    private Vector3 lastMousePos;
	
	void Start()
	{
		currentPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
		pivot.localRotation = Quaternion.Euler(currentPitch, pivot.localRotation.eulerAngles.y, 0f);
	}

    void Update()
    {
        HandleMouseInput();
        HandleZoom();
    }

    void HandleMouseInput()
	{
		// Ignore camera input when pointer is over any UI element
		if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
			return;
	
		// Pan (left click)
		if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
			lastMousePos = Input.mousePosition;
	
		if (Input.GetMouseButton(0))
		{
			Vector3 delta = Input.mousePosition - lastMousePos;
			Vector3 right = cam.right;
			Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
	
			Vector3 move = (right * -delta.x + forward * -delta.y) * panSpeed * Time.deltaTime;
			pivot.Translate(move, Space.World);
			lastMousePos = Input.mousePosition;
		}
	
		// Orbit (right click)
		if (Input.GetMouseButton(1))
		{
			Vector3 delta = Input.mousePosition - lastMousePos;
			float inputY = invertOrbitY ? -delta.y : delta.y;
		
			float rotX = inputY * orbitSpeed * Time.deltaTime;   // degrees
			float rotY =  delta.x * orbitSpeed * Time.deltaTime;
		
			currentPitch = Mathf.Clamp(currentPitch + rotX, minPitch, maxPitch);
			pivot.localRotation = Quaternion.Euler(currentPitch, pivot.localRotation.eulerAngles.y, 0f); // pitch
			pivot.Rotate(Vector3.up, rotY, Space.World); // yaw
		
			lastMousePos = Input.mousePosition;
		}
	}

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 dir = cam.localPosition.normalized;
        float distance = cam.localPosition.magnitude;
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, zoomMin, zoomMax);
        cam.localPosition = dir * distance;
    }
}
