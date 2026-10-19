using UnityEngine;

[RequireComponent(typeof(Light))]
[RequireComponent(typeof(MeshCollider))]
public class SpotlightCollider : MonoBehaviour
{
    private Light spotlight;
    private MeshCollider meshCollider;
    private float lastRange;
    private float lastSpotAngle;

    void Start()
    {
        spotlight = GetComponent<Light>();
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.isTrigger = true;
        meshCollider.convex = true; // required for triggers
        GenerateConeMesh();
    }

    void Update()
    {
        // Auto regenerate if you change light properties at runtime
        if (spotlight.range != lastRange || spotlight.spotAngle != lastSpotAngle)
        {
            GenerateConeMesh();
        }
    }

    void GenerateConeMesh()
    {
        lastRange = spotlight.range;
        lastSpotAngle = spotlight.spotAngle;

        float range = spotlight.range;
        float radius = range * Mathf.Tan(spotlight.spotAngle * 0.5f * Mathf.Deg2Rad);
        int segments = 16; // smoothness of the cone, increase if needed

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];

        // Tip of the cone at the light origin
        vertices[0] = Vector3.zero;
        // Center of the base cap
        vertices[1] = new Vector3(0, 0, range);

        // Base rim vertices
        for (int i = 0; i < segments; i++)
        {
            float rad = (i / (float)segments) * Mathf.PI * 2;
            vertices[i + 2] = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, range);
        }

        // Side triangles (tip to rim)
        for (int i = 0; i < segments; i++)
        {
            int tri = i * 3;
            triangles[tri]     = 0;
            triangles[tri + 1] = 2 + (i + 1) % segments;
            triangles[tri + 2] = 2 + i;
        }

        // Base cap triangles
        for (int i = 0; i < segments; i++)
        {
            int tri = segments * 3 + i * 3;
            triangles[tri]     = 1;
            triangles[tri + 1] = 2 + i;
            triangles[tri + 2] = 2 + (i + 1) % segments;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshCollider.sharedMesh = mesh;
    }
}