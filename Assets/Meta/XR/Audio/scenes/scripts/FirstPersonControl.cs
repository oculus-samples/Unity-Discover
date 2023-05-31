using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonControl : MonoBehaviour
{
    public float movementSpeed = 5.0f;
    public float mouseSensitivity = 5.0f;
    private float headHeight = 1.5f;

    void Update()
    {
        transform.Translate(Input.GetAxis("Horizontal") * movementSpeed * Time.deltaTime, 0.0f,
            Input.GetAxis("Vertical") * movementSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.y = headHeight;
        transform.position = pos;

        transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * mouseSensitivity, Space.World);
        transform.Rotate(Vector3.right, -Input.GetAxis("Mouse Y") * mouseSensitivity, Space.Self);
    }
}
