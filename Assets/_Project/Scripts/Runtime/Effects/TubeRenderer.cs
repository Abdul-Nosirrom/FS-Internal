using System;
using FS.MeshProcessing;
using Pathfinding.Collections;
using TimeUtils;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class TubeRenderer : MonoBehaviour
{
    public Vector2 m_uvTiling = new Vector2(1, 1);
    public int m_maxTrailCount = 128;
    [Range(3, 64)] public int m_ringResolution = 16;
    
    [SerializeField] private MeshFilter m_meshFilter;
    public MeshFilter MeshFilter
    {
        get
        {
            if (m_meshFilter == null) m_meshFilter = GetComponent<MeshFilter>();
            return m_meshFilter;
        }
    }
    
    [SerializeField] private Mesh m_mesh;

    public Mesh Mesh
    {
        get
        {
            if (m_mesh == null)
            {
                m_mesh = new Mesh
                {
                    name = $"TrailMesh_{name}",
                    hideFlags = HideFlags.HideAndDontSave
                };
                m_meshFilter.mesh = m_mesh;
            }
            return m_mesh;
        }
    }

    // Should be circular buffer of structs i.e particle data. To hold stuff like 'age', color (for fade gradient), size, etc..
    // Size can be a param based on a curve. On like 'age'/'normalized age'
    private CircularBuffer<Vector3> m_positions;
    private CircularBuffer<Quaternion> m_normals;
    private CircularBuffer<float> m_distance;
    private CircularBuffer<Vector2> m_uvs;
    private CircularBuffer<float> m_age;
    private CircularBuffer<TimeSince> m_sinceAge;

    private struct TrailEntry
    {
        public Matrix4x4 transform;
        //public TimeSin
    }

    private float m_cumilativeDistance;

    private void OnEnable()
    {
        m_cumilativeDistance = 0;
        EditorApplication.update += LateUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= LateUpdate;
    }

    private void OnValidate()
    {
        m_positions = new CircularBuffer<Vector3>(m_maxTrailCount);
        m_normals = new CircularBuffer<Quaternion>(m_maxTrailCount);
        m_uvs = new CircularBuffer<Vector2>(m_maxTrailCount);
        m_age = new CircularBuffer<float>(m_maxTrailCount);
        m_sinceAge = new CircularBuffer<TimeSince>(m_maxTrailCount);
    }

    private void LateUpdate()
    {
        Vector3 prevPosition = transform.position;
        if (m_positions.Length > 0) prevPosition = m_positions.Last;
        m_cumilativeDistance += Vector3.Distance(prevPosition, transform.position);

        if (m_positions.Length > m_maxTrailCount)
        {
            m_positions.PopStart();
            m_normals.PopStart();
            m_age.PopStart();
            m_distance.PopStart();
            m_sinceAge.PopStart();
        }
        m_positions.PushEnd(transform.position);
        m_normals.PushEnd(transform.rotation);
        m_age.PushEnd(Time.time);
        m_distance.PushEnd(m_cumilativeDistance);
        m_sinceAge.PushEnd(Time.time);
        
        Debug.Log($"Positions Length: {m_positions.Length}");

        BuildMesh();
    }

    private Vector3[] m_vertexPositions;
    private int[] m_vertexIndices;
    private Vector3[] m_vertexNormals;
    private Vector2[] m_vertexUVs;
    private Vector4[] m_vertexColors;
    
    private void BuildMesh()
    {
        if (m_vertexPositions == null)
        {
            m_vertexPositions = new Vector3[m_maxTrailCount * (m_ringResolution + 1)];
            m_vertexNormals = new Vector3[m_maxTrailCount * (m_ringResolution + 1)];
            m_vertexUVs = new Vector2[m_maxTrailCount * (m_ringResolution + 1)];
            m_vertexColors = new Vector4[m_maxTrailCount * (m_ringResolution + 1)];
            m_vertexIndices = new int[(m_maxTrailCount - 1) * m_ringResolution * 6];
        }
        
        // Need at least 2 positions to build a tube
        if (m_positions.Length < 2)
        {
            Mesh.Clear();
            return;
        }
        
        int vertsPerRing = m_ringResolution + 1;
        int totalVerts = m_positions.Length * vertsPerRing;
        int totalTris = (m_positions.Length - 1) * m_ringResolution * 6;

        m_vertexPositions = new Vector3[totalVerts];
        m_vertexNormals = new Vector3[totalVerts];
        m_vertexUVs = new Vector2[totalVerts];
        m_vertexIndices = new int[totalTris];
        
        // Build vertices for each ring
        for (int i = 0; i < m_positions.Length; i++)
        {
            var prevPos = m_positions[i == 0 ? i : i - 1];
            var nextPos = m_positions[i == 0 ? i + 1 : i];
            var properNormal = Vector3.Normalize(nextPos - prevPos);
            
            Vector3 centerPos = transform.worldToLocalMatrix.MultiplyPoint(m_positions[i]);
            Quaternion centerRot = m_normals[i];
            float distance = m_distance[i];
            float size = 1;
            
            for (int j = 0; j <= m_ringResolution; j++)
            {
                int vertIndex = i * vertsPerRing + j;
                
                float normalizedAngle = j / (float)m_ringResolution;
                Vector2 uv = new Vector2(distance * m_uvTiling.x % 1, normalizedAngle * m_uvTiling.y % 1);

                float angle = 2 * Mathf.PI * normalizedAngle;
                Vector3 localPos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 localNormal = localPos.normalized;
                
                Matrix4x4 localToWorld = Matrix4x4.TRS(centerPos, centerRot, size * Vector3.one);
                Vector3 worldPos = localToWorld.MultiplyPoint(localPos);
                Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;

                m_vertexPositions[vertIndex] = worldPos;
                m_vertexNormals[vertIndex] = worldNormal;
                m_vertexUVs[vertIndex] = uv;
            }
        }
        
        // Build triangles connecting adjacent rings
        int triIndex = 0;
        for (int i = 0; i < m_positions.Length - 1; i++)
        {
            for (int j = 0; j < m_ringResolution; j++)
            {
                int curr = i * vertsPerRing + j;
                int next = curr + 1;
                int currNextRing = curr + vertsPerRing;
                int nextNextRing = next + vertsPerRing;
                
                // Quad as two triangles (check winding order for your needs)
                m_vertexIndices[triIndex++] = curr;
                m_vertexIndices[triIndex++] = next;
                m_vertexIndices[triIndex++] = currNextRing;
                
                m_vertexIndices[triIndex++] = next;
                m_vertexIndices[triIndex++] = nextNextRing;
                m_vertexIndices[triIndex++] = currNextRing;
            }
        }

        Mesh.Clear();
        Mesh.vertices = m_vertexPositions;
        Mesh.normals = m_vertexNormals;
        Mesh.uv = m_vertexUVs;
        Mesh.triangles = m_vertexIndices;
        Mesh.RecalculateBounds();
        MeshFilter.mesh = Mesh;
    }
}