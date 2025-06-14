using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EyeTrackingRayScript : MonoBehaviour
{
    [SerializeField]
    private float rayDist = 5.0f;
    
    [SerializeField]
    private float rayWidth = 0.01f;

    [SerializeField]
    private LayerMask layersToInclude;

    [SerializeField]
    private Color rayColorDefaultState = Color.yellow;

    [SerializeField]
    private Color rayColorHoverState = Color.red;

    [SerializeField]
    private GameObject reticle;
    
    private LineRenderer lineRenderer;
    private List<EyeInteractable> eyeIntetactablesList = new List<EyeInteractable>();

    // Start is called before the first frame update
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        SetupRay();
    }

    private void SetupRay()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;
        lineRenderer.startColor = rayColorDefaultState;
        lineRenderer.endColor = rayColorDefaultState;
        lineRenderer.SetPosition(0, transform.position + Vector3.down * 0.05f);
        lineRenderer.SetPosition(1, transform.position + Vector3.forward * rayDist);
    }

    private void FixedUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward) * rayDist, out hit, Mathf.Infinity, layersToInclude) && null != hit.transform.GetComponent<EyeInteractable>())
        {
            UnSelect();
            lineRenderer.startColor = rayColorHoverState;
            lineRenderer.endColor = rayColorHoverState;
            var eyeInteractable = hit.transform.GetComponent<EyeInteractable>();
            eyeIntetactablesList.Add(eyeInteractable);
            eyeInteractable.IsHovered = true;
        }
        else
        {
            lineRenderer.startColor = rayColorDefaultState;
            lineRenderer.endColor = rayColorDefaultState;
            UnSelect(true);
        }

    }

    private void Update()
    {
        Debug.Log(string.Join(", ", eyeIntetactablesList));
    }

    void UnSelect(bool clear = false)
    {
        foreach(var eyeInteractable in eyeIntetactablesList)
        {
            eyeInteractable.IsHovered = false;
        }
        if(clear)
        {
            eyeIntetactablesList.Clear();
        }
    }
}
