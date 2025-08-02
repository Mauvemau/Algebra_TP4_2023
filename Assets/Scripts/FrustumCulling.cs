using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class MyCamera {
    [Range(0f, 179f)]
    [SerializeField] private float fov = 60f;
    [Min(.1f)]
    [SerializeField] private float aspect = 16f / 9f;
    [Min(.1f)]
    [SerializeField] private float near = 0.1f;
    [Min(.2f)]
    [SerializeField] private float far = 25f;

    private Vector3 _position = Vector3.zero;
    private Vector3 _direction = Vector3.forward;
    private Vector3 _up = Vector3.up;
    
    public float FOV { get => fov; set => fov = value; }
    public float Aspect { get => aspect; set => aspect = value; }
    public float Near { get => near; set => near = value; }
    public float Far { get => far; set => far = value; }
    
    public Vector3 Position { get => _position; set => _position = value; }
    public Vector3 Direction { get => _direction; set => _direction = value.normalized; }
    
    public bool PointInFrustum(Vector3 point) {
        var planes = CalculateFrustumPlanes();
        foreach (var plane in planes)
        {
            if (plane.GetDistanceToPoint(point) < 0f)
                return false;
        }
        return true;
    }

    public bool AnyPointInFrustum(Vector3[] points) {
        return points.Any(PointInFrustum);
    }
    
    public Vector3[] GetFrustumCornersWorld() {
        var corners = new Vector3[8];

        var fovRad = Mathf.Deg2Rad * fov;
        var tanFov = Mathf.Tan(fovRad * 0.5f);

        var nearHeight = 2f * tanFov * near;
        var nearWidth = nearHeight * aspect;
        var farHeight = 2f * tanFov * far;
        var farWidth = farHeight * aspect;

        var forward = _direction.normalized;
        var right = Vector3.Cross(forward, _up).normalized;
        var up = Vector3.Cross(right, forward).normalized;

        var nearCenter = _position + forward * near;
        var farCenter = _position + forward * far;

        // Near
        corners[0] = nearCenter - (right * (nearWidth * 0.5f)) - (up * (nearHeight * 0.5f)); // bottom left
        corners[1] = nearCenter + (right * (nearWidth * 0.5f)) - (up * (nearHeight * 0.5f)); // bottom right
        corners[2] = nearCenter - (right * (nearWidth * 0.5f)) + (up * (nearHeight * 0.5f)); // top left
        corners[3] = nearCenter + (right * (nearWidth * 0.5f)) + (up * (nearHeight * 0.5f)); // top right

        // Far
        corners[4] = farCenter - (right * (farWidth * 0.5f)) - (up * (farHeight * 0.5f)); // bottom left
        corners[5] = farCenter + (right * (farWidth * 0.5f)) - (up * (farHeight * 0.5f)); // bottom right
        corners[6] = farCenter - (right * (farWidth * 0.5f)) + (up * (farHeight * 0.5f)); // top left
        corners[7] = farCenter + (right * (farWidth * 0.5f)) + (up * (farHeight * 0.5f)); // top right

        return corners;
    }
    
    public Plane[] CalculateFrustumPlanes() {
        var corners = GetFrustumCornersWorld();
        var planes = new Plane[6];

        planes[0] = new Plane(corners[0], corners[4], corners[6]); // left
        planes[1] = new Plane(corners[5], corners[1], corners[7]); // right
        planes[2] = new Plane(corners[0], corners[1], corners[4]); // bottom
        planes[3] = new Plane(corners[2], corners[6], corners[3]); // top
        planes[4] = new Plane(corners[0], corners[2], corners[1]); // near
        planes[5] = new Plane(corners[5], corners[7], corners[4]); // far

        return planes;
    }
}

public class FrustumCulling : MonoBehaviour {
    [Header("Camera Settings")]
    [SerializeField] private MyCamera cam;
    [Header("Culling Settings")]
    [SerializeField] private bool vertexCheck = false;
    [SerializeField] private List<GameObject> objectsToCull;
    
    private List<(Transform transform, MeshFilter filter, MeshRenderer meshRenderer)> _cachedMeshes;
    
    private static Bounds GetMeshBounds(Mesh mesh) {
        var vertices = mesh.vertices;

        if (vertices.Length == 0) {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        var min = vertices[0];
        var max = vertices[0];

        for (var i = 1; i < vertices.Length; i++) {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        var bounds = new Bounds((max + min) / 2f, max - min);
        return bounds;
    }

    /// <summary>
    /// Returns an array with the vertex from a mesh's bounding box in their world position
    /// </summary>
    private Vector3[] GetMeshBoundsVertex(Mesh mesh, Transform objTransform) {
        var bounds = GetMeshBounds(mesh);
        var center = bounds.center;
        var extents = bounds.extents;
        
        var corners = new Vector3[8];

        corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
        corners[1] = center + new Vector3(extents.x, -extents.y, -extents.z);
        corners[2] = center + new Vector3(-extents.x, -extents.y, extents.z);
        corners[3] = center + new Vector3(extents.x, -extents.y, extents.z);
        corners[4] = center + new Vector3(-extents.x, extents.y, -extents.z);
        corners[5] = center + new Vector3(extents.x, extents.y, -extents.z);
        corners[6] = center + new Vector3(-extents.x, extents.y, extents.z);
        corners[7] = center + new Vector3(extents.x, extents.y, extents.z);
        
        for (var i = 0; i < corners.Length; i++) {
            corners[i] = objTransform.TransformPoint(corners[i]); // Transform to world position
        }

        return corners;
    }

    /// <summary>
    /// Returns an array with the vertex from a mesh in their world position
    /// </summary>
    private Vector3[] GetMeshVertex(Mesh mesh, Transform objTransform) {
        var meshVertex = new Vector3[mesh.vertexCount];

        for (var i = 0; i < mesh.vertices.Length; i++) {
            meshVertex[i] = objTransform.TransformPoint(mesh.vertices[i]);
        }

        return meshVertex;
    }
    
    private void CullObjects() {
        foreach (var cMesh in _cachedMeshes) {
            var mf = cMesh.filter;
            if (!mf) {
                continue;
            }

            var mr = cMesh.meshRenderer;
            if (!mr) {
                continue;
            }

            var boundingBoxVertex = GetMeshBoundsVertex(mf.mesh, cMesh.transform);
            if (cam.AnyPointInFrustum(boundingBoxVertex)) {
                if (vertexCheck) {
                    var meshVertex = GetMeshVertex(mf.mesh, cMesh.transform);
                    mr.enabled = cam.AnyPointInFrustum(meshVertex);
                }
                else {
                    mr.enabled = true;
                }
            }
            else {
                mr.enabled = false;
            }
        }
    }

    private void DrawFrustumGizmos() {
        if (cam == null) return;
        Gizmos.color = Color.cyan;
        
        var corners = cam.GetFrustumCornersWorld();

        // Near
        Gizmos.DrawLine(corners[0], corners[1]); // bottom
        Gizmos.DrawLine(corners[1], corners[3]); // right
        Gizmos.DrawLine(corners[3], corners[2]); // top
        Gizmos.DrawLine(corners[2], corners[0]); // left

        // Far
        Gizmos.DrawLine(corners[4], corners[5]);
        Gizmos.DrawLine(corners[5], corners[7]);
        Gizmos.DrawLine(corners[7], corners[6]);
        Gizmos.DrawLine(corners[6], corners[4]);

        // Near to far
        Gizmos.DrawLine(corners[0], corners[4]);
        Gizmos.DrawLine(corners[1], corners[5]);
        Gizmos.DrawLine(corners[2], corners[6]);
        Gizmos.DrawLine(corners[3], corners[7]);
    }

    private void Awake() {
        _cachedMeshes = new List<(Transform transform, MeshFilter filter, MeshRenderer meshRenderer)>();
        foreach (var obj in objectsToCull) {
            var mf = obj.GetComponent<MeshFilter>();
            var mr = obj.GetComponent<MeshRenderer>();
            _cachedMeshes.Add((obj.transform,mf,mr));
        }
    }
    
    private void Update() {
        cam.Position = transform.position;
        cam.Direction = transform.forward;
        CullObjects();
    }

    private void OnDrawGizmos() {
        DrawFrustumGizmos();
        if (Application.isPlaying) return;
        cam.Position = transform.position;
        cam.Direction = transform.forward;
    }
}
