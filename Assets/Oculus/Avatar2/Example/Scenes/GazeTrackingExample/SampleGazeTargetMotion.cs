using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleGazeTargetMotion : MonoBehaviour
{
    [SerializeField]
    private float _magnitudeX = 0f;
    [SerializeField]
    private float _magnitudeY = 0f;
    [SerializeField]
    private float _magnitudeZ = 0f;

    [SerializeField]
    private float _speedX = 1f;
    [SerializeField]
    private float _speedY = 1f;
    [SerializeField]
    private float _speedZ = 1f;

    private Vector3 _startPos;

    void Awake()
    {
        _startPos = transform.localPosition;
    }

    void Update()
    {
        var t = transform;
        float radians = Time.time * Mathf.PI;

        // Only update axis that are actually moving - so that we can drag in the editor when its stationary
        Vector3 newPos = t.localPosition;
        newPos.x = _magnitudeX > 0f ? _startPos.x + Mathf.Sin(radians * _speedX) * _magnitudeX : newPos.x;
        newPos.y = _magnitudeY > 0f ? _startPos.y + Mathf.Sin(radians * _speedY) * _magnitudeY : newPos.y;
        newPos.z = _magnitudeZ > 0f ? _startPos.z + Mathf.Sin(radians * _speedZ) * _magnitudeZ : newPos.z;

        t.localPosition = newPos;
    }
}
