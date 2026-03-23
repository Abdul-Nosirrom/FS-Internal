using System;
using System.Collections.Generic;
using System.Linq;
using Datamodel;
using Datamodel.Vmap;
using FS.MeshProcessing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using SysVector2 = System.Numerics.Vector2;
using SysVector3 = System.Numerics.Vector3;

namespace FS.VmapImport
{
    /// <summary>
    /// Converts Source 2 <see cref="CDmePolygonMesh"/> half-edge data into Unity Meshes
    /// and PreservedMesh topology structures.
    ///
    /// Also provides content hashing for mesh deduplication: two meshes with identical
    /// geometry (positions, normals, UVs, indices) produce the same hash and share a
    /// single Mesh asset - even if they were created independently in Hammer.
    /// </summary>
    public static class VmapMeshBuilder
    {
        #region Intermediate Structures

        /// <summary> A single corner of a face, referencing position data and per-corner UV/normal data. </summary>
        private struct FaceCorner
        {
            public int vertexIndex;       // Index into position data stream
            public int faceVertexIndex;   // Index into per-corner UV/normal streams
        }

        /// <summary> A face extracted from the half-edge walk, ready for triangulation. </summary>
        private struct ExtractedFace
        {
            public FaceCorner[] corners;
            public int materialIndex;     // Index into the Materials[] string array
        }

        #endregion

        #region Public API - High-Level

        /// <summary>
        /// Creates a complete mesh GameObject from a <see cref="CMapMesh"/> node:
        /// MeshFilter, MeshRenderer (with resolved materials), optional MeshCollider,
        /// static flags, surface tagging, and topology preservation.
        ///
        /// Uses content-based mesh deduplication - identical geometry shares a single Mesh asset.
        /// This is the main entry point for both the importer and handlers.
        /// </summary>
        public static GameObject BuildMeshGameObject(CMapMesh meshNode, Transform parent, VmapImportContext ctx)
        {
            CDmePolygonMesh polyMesh = meshNode.MeshData;
            if (polyMesh == null) return null;

            string meshName = ctx.GetNodeDisplayName(meshNode, "mesh");

            // Get or build the Unity Mesh (deduplicated by content hash)
            Mesh unityMesh = GetOrBuildMesh(polyMesh, meshName, ctx, out List<string> materialPaths);
            if (unityMesh == null) return null;

            // Create the GameObject hierarchy
            var go = new GameObject(meshName);
            go.transform.SetParent(parent, false);
            VmapNodeProcessor.ApplyMapNodeTransform(meshNode, go, ctx);

            // Rendering
            go.AddComponent<MeshFilter>().sharedMesh = unityMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = ctx.Materials.Resolve(materialPaths);

            // Collision
            if (ctx.Settings.m_addColliders)
                go.AddComponent<MeshCollider>().sharedMesh = unityMesh;

            // Static flags - per-mesh based on Hammer's BakeLighting property
            if (ctx.Settings.m_markStatic && VmapClassification.ShouldBeStatic(meshNode))
                go.isStatic = true;

            // Surface tagging - applies Unity layer by name
            string surfaceTag = VmapClassification.GetSurfaceTag(meshNode);
            if (surfaceTag != null)
                ApplySurfaceTag(go, surfaceTag);

            // Topology preservation - creates PreservedMesh sub-asset for runtime edge tools
            if (ctx.Settings.m_preserveTopology && VmapClassification.ShouldPreserveTopology(meshNode))
            {
                var preserved = BuildPreservedMesh(polyMesh, ctx);
                if (preserved != null)
                {
                    var preservedAsset = ScriptableObject.CreateInstance<PreservedMeshAsset>();
                    preservedAsset.SetMesh(preserved);
                    ctx.AddSubAsset($"[Preserved Topology] {meshName}", preservedAsset);
                    go.AddComponent<MeshTopologyPreserver>().SetMesh(preservedAsset);
                    go.AddComponent<MeshEdgeSelector>();
                }
            }

            return go;
        }

        /// <summary>
        /// Builds a mesh GO that's a trigger collider only - no renderer.
        /// Used by TriggerHandler for brush entities where the mesh defines trigger volume.
        /// </summary>
        public static GameObject BuildTriggerColliderGameObject(CMapMesh meshNode, Transform parent, VmapImportContext ctx)
        {
            CDmePolygonMesh polyMesh = meshNode.MeshData;
            if (polyMesh == null) return null;

            string meshName = ctx.GetNodeDisplayName(meshNode, "trigger_mesh");

            Mesh unityMesh = GetOrBuildMesh(polyMesh, meshName, ctx, out _);
            if (unityMesh == null) return null;

            var go = new GameObject(meshName);
            go.transform.SetParent(parent, false);
            VmapNodeProcessor.ApplyMapNodeTransform(meshNode, go, ctx);

            // Trigger collider - convex so it can be a trigger, no renderer
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = unityMesh;
            collider.convex = true;
            collider.isTrigger = true;

            return go;
        }

        #endregion

        #region Public API - Mesh Building with Dedup

        /// <summary>
        /// Gets a cached mesh or builds a new one. Content-hash deduplication ensures
        /// identical geometry shares a single Mesh asset, regardless of where it appears.
        ///
        /// The hash is computed from the raw CDmePolygonMesh arrays (vertex indices, edge indices,
        /// face indices, material indices). Two independently-created cubes with the same
        /// vertex layout will produce the same hash and share one Mesh.
        /// </summary>
        public static Mesh GetOrBuildMesh(CDmePolygonMesh polyMesh, string meshName, VmapImportContext ctx, out List<string> materialPaths)
        {
            int contentHash = ComputeContentHash(polyMesh);

            // Check cache - if we've already built this exact geometry, reuse it TODO: Deduplication is broken, so disabled at the moment till hash collisions can be resolved
            // if (ctx.MeshContentCache.TryGetValue(contentHash, out Mesh cached))
            // {
            //     materialPaths = ctx.MeshMaterialCache.GetValueOrDefault(contentHash, new List<string>());
            //     return cached;
            // }

            // Build new mesh
            Mesh mesh = BuildUnityMesh(polyMesh, ctx, out materialPaths);
            if (mesh == null)
            {
                materialPaths = new List<string>();
                return null;
            }

            mesh.name = meshName;
            ctx.AddSubAssetExplicit(meshName, mesh);

            // Track materials for discovery
            foreach (string matPath in materialPaths)
                ctx.DiscoveredMaterials.Add(matPath);

            // Cache for dedup
            ctx.MeshContentCache[contentHash] = mesh;
            ctx.MeshMaterialCache[contentHash] = materialPaths;

            return mesh;
        }

        #endregion

        #region Content Hashing

        /// <summary>
        /// Computes a content hash from the CDmePolygonMesh's structural arrays.
        /// Two meshes with identical vertex layout, edge connectivity, face structure,
        /// and material indices produce the same hash.
        /// </summary>
        private static int ComputeContentHash(CDmePolygonMesh polyMesh)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + HashIntArray(polyMesh.VertexDataIndices);
                hash = hash * 31 + HashIntArray(polyMesh.EdgeVertexIndices);
                hash = hash * 31 + HashIntArray(polyMesh.EdgeNextIndices);
                hash = hash * 31 + HashIntArray(polyMesh.FaceEdgeIndices);
                hash = hash * 31 + HashIntArray(polyMesh.FaceDataIndices);
                hash = hash * 31 + HashIntArray(polyMesh.EdgeVertexDataIndices);
                hash = hash * 31 + polyMesh.Materials.Count;

                // Include position data in the hash for robustness
                // (two meshes might have identical topology but different positions)
                var positions = GetStreamVector3(polyMesh.VertexData, "position");
                if (positions != null)
                {
                    foreach (var p in positions)
                    {
                        hash = hash * 31 + p.X.GetHashCode();
                        hash = hash * 31 + p.Y.GetHashCode();
                        hash = hash * 31 + p.Z.GetHashCode();
                    }
                }

                return hash;
            }
        }

        /// <summary> Hashes all values in a Datamodel IntArray. </summary>
        private static int HashIntArray(IntArray array)
        {
            if (array == null || array.Count == 0) return 0;
            unchecked
            {
                int hash = array.Count;
                foreach (int val in array)
                    hash = hash * 31 + val;
                return hash;
            }
        }

        #endregion

        #region Core Mesh Building

        /// <summary>
        /// Builds a Unity Mesh from a typed CDmePolygonMesh by walking the half-edge structure.
        /// Returns the material paths found in the mesh data (ordered to match submesh indices).
        /// </summary>
        public static Mesh BuildUnityMesh(CDmePolygonMesh polyMesh, VmapImportContext ctx, out List<string> materialPaths)
        {
            materialPaths = new List<string>();

            // Convert Datamodel IntArrays to int[] via foreach
            // (Datamodel.Array's non-generic IList indexer throws NotImplementedException)
            var vertexDataIndices = ToIntArray(polyMesh.VertexDataIndices);
            var edgeNextIndices = ToIntArray(polyMesh.EdgeNextIndices);
            var edgeVertexIndices = ToIntArray(polyMesh.EdgeVertexIndices);
            var edgeVertexDataIndices = ToIntArray(polyMesh.EdgeVertexDataIndices);
            var faceEdgeIndices = ToIntArray(polyMesh.FaceEdgeIndices);
            var faceDataIndices = ToIntArray(polyMesh.FaceDataIndices);

            // Vertex data streams (positions, normals - shared across all corners that reference the same vertex)
            var positions = GetStreamVector3(polyMesh.VertexData, "position");
            var normals = GetStreamVector3(polyMesh.VertexData, "normal");

            // Per-corner data streams (UVs, normals - unique per face-corner even if vertices are shared)
            var texcoords = GetStreamVector2(polyMesh.FaceVertexData, "texcoord");
            var faceNormals = GetStreamVector3(polyMesh.FaceVertexData, "normal");

            // Material paths (.vmat references)
            foreach (var mat in polyMesh.Materials)
                materialPaths.Add(mat?.ToString() ?? "");

            // Per-face material indices via the FaceData "materialindex" stream
            // Chain: face f → FaceDataIndices[f] → materialindex stream[dataIdx] → Materials[matIdx]
            var faceMaterialIndices = GetFaceMaterialIndices(polyMesh.FaceData, faceDataIndices);

            // Walk each face via half-edge loops to extract corners
            int faceCount = faceEdgeIndices.Length;
            var extractedFaces = new List<ExtractedFace>(faceCount);

            for (int f = 0; f < faceCount; f++)
            {
                int startEdge = faceEdgeIndices[f];
                int matIndex = (f < faceMaterialIndices.Length) ? faceMaterialIndices[f] : 0;

                // Walk the half-edge loop: follow EdgeNextIndices until we return to startEdge
                var corners = new List<FaceCorner>();
                int currentEdge = startEdge;
                int safety = 0;
                do
                {
                    int vertIdx = edgeVertexIndices[currentEdge];
                    int dataIdx = (vertexDataIndices.Length > 0 && vertIdx < vertexDataIndices.Length)
                        ? vertexDataIndices[vertIdx] : vertIdx;
                    int faceVertIdx = (edgeVertexDataIndices.Length > 0 && currentEdge < edgeVertexDataIndices.Length)
                        ? edgeVertexDataIndices[currentEdge] : -1;

                    corners.Add(new FaceCorner
                    {
                        vertexIndex = dataIdx,
                        faceVertexIndex = faceVertIdx
                    });

                    currentEdge = edgeNextIndices[currentEdge];
                    safety++;
                } while (currentEdge != startEdge && safety < 256);

                if (corners.Count >= 3)
                {
                    extractedFaces.Add(new ExtractedFace
                    {
                        corners = corners.ToArray(),
                        materialIndex = matIndex
                    });
                }
            }

            return AssembleUnityMesh(extractedFaces, positions, normals, texcoords, faceNormals,
                materialPaths.Count, ctx);
        }

        
        static float sqrMagF3(float3 val) => val.x * val.x + val.y * val.y + val.z * val.z;
        static float3 normalizedF3(float3 val) => val / Mathf.Sqrt(sqrMagF3(val));
        static float3 vec3ToFloat3(Vector3 val) => new float3(val.x, val.y, val.z);

        /// <summary>
        /// Builds a PreservedMesh from a Source 2 CDmePolygonMesh for topology-aware runtime
        /// operations (edge loop selection, spline generation from edge chains).
        ///
        /// Source 2 stores positions per-vertex (shared) and normals per-face-corner
        /// (in FaceVertexData). This function splits shared vertices at normal discontinuities
        /// so that adjacent faces with different normals produce different vertex indices.
        /// This maps directly to how BakeHalfEdges uses EdgeKey for twin matching:
        ///   - Same normal across an edge → shared vertex index → EdgeKey matches → twin linked (smooth)
        ///   - Different normals across an edge → split vertex indices → EdgeKey differs → boundary (hard edge)
        ///
        /// Open edges (no adjacent face) are also boundaries since there is no opposing
        /// face to produce a matching EdgeKey.
        ///
        /// Falls back to shared vertices with Newell's method normals when FaceVertexData
        /// normals are absent.
        /// </summary>
        public static PreservedMesh BuildPreservedMesh(CDmePolygonMesh polyMesh, VmapImportContext ctx)
        {
            var vertexDataIndices     = ToIntArray(polyMesh.VertexDataIndices);
            var edgeNextIndices       = ToIntArray(polyMesh.EdgeNextIndices);
            var edgeVertexIndices     = ToIntArray(polyMesh.EdgeVertexIndices);
            var faceEdgeIndices       = ToIntArray(polyMesh.FaceEdgeIndices);
            var edgeVertexDataIndices = ToIntArray(polyMesh.EdgeVertexDataIndices);

            var positions = GetStreamVector3(polyMesh.VertexData, "position");
            var fvNormals = GetStreamVector3(polyMesh.FaceVertexData, "normal");

            int edgeCount    = edgeNextIndices.Length;
            int faceCount    = faceEdgeIndices.Length;
            int sharedCount  = vertexDataIndices.Length > 0 ? vertexDataIndices.Length : (positions?.Count ?? 0);
            bool hasFVNormals = fvNormals != null && fvNormals.Count > 0 && edgeVertexDataIndices.Length > 0;

            // --- Step 1: Build shared positions ---
            var sharedPositions = new Vector3[sharedCount];
            for (int i = 0; i < sharedCount; i++)
            {
                int dataIdx = vertexDataIndices.Length > 0 ? vertexDataIndices[i] : i;
                sharedPositions[i] = (positions != null && dataIdx < positions.Count)
                    ? ctx.ConvertPosition(positions[dataIdx]) : Vector3.zero;
            }

            // --- Step 2: Build per-edge remap (shared vertex → split vertex) ---
            // For each half-edge, determine which split vertex it should use.
            // Edges sharing a vertex AND a normal get the same split index.
            // Edges sharing a vertex but with a different normal get a new index.

            int[] edgeSplitVertex = new int[edgeCount]; // maps each half-edge → final vertex index
            var finalVertices = new List<Vertex>();

            if (hasFVNormals)
            {
                // Group half-edges by their shared vertex
                var vertexEdges = new Dictionary<int, List<int>>(); // sharedVert → list of edge indices
                for (int e = 0; e < edgeCount; e++)
                {
                    int sv = edgeVertexIndices[e];
                    if (!vertexEdges.ContainsKey(sv))
                        vertexEdges[sv] = new List<int>();
                    vertexEdges[sv].Add(e);
                }

                // For each shared vertex, group its edges by normal and assign split indices
                foreach (var kvp in vertexEdges)
                {
                    int sv = kvp.Key;
                    var edges = kvp.Value;
                    var normalGroups = new List<(Vector3 normal, int splitIdx)>();

                    foreach (int e in edges)
                    {
                        int fvdIdx = edgeVertexDataIndices[e];
                        Vector3 normal = (fvdIdx >= 0 && fvdIdx < fvNormals.Count)
                            ? ctx.ConvertDirection(fvNormals[fvdIdx]).normalized
                            : Vector3.up;

                        // Find existing group with matching normal
                        int found = -1;
                        for (int g = 0; g < normalGroups.Count; g++)
                        {
                            if (Vector3.Dot(normalGroups[g].normal, normal) > 0.999f)
                            {
                                found = g;
                                break;
                            }
                        }

                        if (found >= 0)
                        {
                            edgeSplitVertex[e] = normalGroups[found].splitIdx;
                        }
                        else
                        {
                            int newIdx = finalVertices.Count;
                            finalVertices.Add(new Vertex
                            {
                                m_position = sharedPositions[sv],
                                m_normal = normal,
                                m_numFaces = 0
                            });
                            normalGroups.Add((normal, newIdx));
                            edgeSplitVertex[e] = newIdx;
                        }
                    }
                }
            }
            else
            {
                // No FVD normals — use shared vertices directly, normals computed later
                for (int i = 0; i < sharedCount; i++)
                {
                    finalVertices.Add(new Vertex
                    {
                        m_position = sharedPositions[i],
                        m_normal = Vector3.zero,
                        m_numFaces = 0
                    });
                }
                for (int e = 0; e < edgeCount; e++)
                    edgeSplitVertex[e] = edgeVertexIndices[e];
            }

            // --- Step 3: Build faces using split vertex indices ---
            var faces = new Face[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                int startEdge = faceEdgeIndices[f];
                var faceVerts = new List<int>();
                int currentEdge = startEdge;
                int safety = 0;
                do
                {
                    faceVerts.Add(edgeSplitVertex[currentEdge]);
                    currentEdge = edgeNextIndices[currentEdge];
                    safety++;
                } while (currentEdge != startEdge && safety < 256);

                faces[f] = new Face { m_vertexIndices = faceVerts.ToArray() };
            }

            var vertices = finalVertices.ToArray();

            foreach (var face in faces)
                foreach (int v in face.m_vertexIndices)
                    if (v < vertices.Length) vertices[v].m_numFaces++;

            // --- Newell's fallback for shared-vertex path ---
            if (!hasFVNormals)
            {
                for (int f = 0; f < faces.Length; f++)
                {
                    var verts = faces[f].m_vertexIndices;
                    if (verts.Length < 3) continue;

                    Vector3 n = Vector3.zero;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        var curr = vertices[verts[i]].m_position;
                        var next = vertices[verts[(i + 1) % verts.Length]].m_position;
                        n.x += (curr.y - next.y) * (curr.z + next.z);
                        n.y += (curr.z - next.z) * (curr.x + next.x);
                        n.z += (curr.x - next.x) * (curr.y + next.y);
                    }
                    n = -n.normalized;
                    foreach (int v in verts)
                        if (v < vertices.Length) vertices[v].m_normal += vec3ToFloat3(n);
                }

                for (int i = 0; i < vertices.Length; i++)
                    vertices[i].m_normal = sqrMagF3(vertices[i].m_normal) > 0.0001f
                        ? normalizedF3(vertices[i].m_normal) : Vector3.up;
            }

            int quadCount = 0, triCount = 0, nGonCount = 0;
            foreach (var face in faces)
            {
                switch (face.m_vertexIndices.Length)
                {
                    case 3: triCount++; break;
                    case 4: quadCount++; break;
                    default: nGonCount++; break;
                }
            }

            return new PreservedMesh
            {
                m_vertices = vertices,
                m_faces = faces,
                m_quadCount = quadCount,
                m_triangleCount = triCount,
                m_nGonCount = nGonCount
            };
        }

        #endregion

        #region Mesh Assembly

        /// <summary>
        /// Assembles a Unity Mesh from extracted faces: deduplicates vertices per unique
        /// (position, UV/normal) combination, fan-triangulates N-gons, and groups triangles
        /// into submeshes by material index.
        /// </summary>
        private static Mesh AssembleUnityMesh(
            List<ExtractedFace> faces,
            List<SysVector3> positions, List<SysVector3> normals,
            List<SysVector2> texcoords, List<SysVector3> faceNormals,
            int materialCount, VmapImportContext ctx)
        {
            var unityPositions = new List<Vector3>();
            var unityNormals = new List<Vector3>();
            var unityUVs = new List<Vector2>();

            int submeshCount = Mathf.Max(materialCount, 1);
            var submeshTriangles = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++)
                submeshTriangles[i] = new List<int>();

            // Vertex dedup: (positionDataIndex, perCornerDataIndex) → Unity vertex index
            var vertexCache = new Dictionary<(int, int), int>();

            foreach (var face in faces)
            {
                int cornerCount = face.corners.Length;
                int[] unityIndices = new int[cornerCount];

                for (int c = 0; c < cornerCount; c++)
                {
                    var corner = face.corners[c];
                    var key = (corner.vertexIndex, corner.faceVertexIndex);

                    if (!vertexCache.TryGetValue(key, out int unityIdx))
                    {
                        unityIdx = unityPositions.Count;
                        vertexCache[key] = unityIdx;

                        var pos = (positions != null && corner.vertexIndex < positions.Count)
                            ? ctx.ConvertPosition(positions[corner.vertexIndex]) : Vector3.zero;
                        unityPositions.Add(pos);

                        Vector3 normal = Vector3.up;
                        if (faceNormals != null && corner.faceVertexIndex >= 0 && corner.faceVertexIndex < faceNormals.Count)
                            normal = ctx.ConvertDirection(faceNormals[corner.faceVertexIndex]).normalized;
                        else if (normals != null && corner.vertexIndex < normals.Count)
                            normal = ctx.ConvertDirection(normals[corner.vertexIndex]).normalized;
                        unityNormals.Add(normal);

                        Vector2 uv = Vector2.zero;
                        if (texcoords != null && corner.faceVertexIndex >= 0 && corner.faceVertexIndex < texcoords.Count)
                        {
                            var src = texcoords[corner.faceVertexIndex];
                            uv = new Vector2(src.X, 1f - src.Y); // V-flip for Unity
                        }
                        unityUVs.Add(uv);
                    }
                    unityIndices[c] = unityIdx;
                }

                // Fan triangulation: [0, i+1, i] winding
                // Works correctly for convex N-gons. Concave faces may produce incorrect triangles.
                // TODO: Replace with ear-clipping for concave polygon support.
                int matIdx = Mathf.Clamp(face.materialIndex, 0, submeshCount - 1);
                var tris = submeshTriangles[matIdx];
                for (int i = 1; i < cornerCount - 1; i++)
                {
                    tris.Add(unityIndices[0]);
                    tris.Add(unityIndices[i + 1]);
                    tris.Add(unityIndices[i]);
                }
            }

            var mesh = new Mesh();
            if (unityPositions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(unityPositions);
            mesh.SetNormals(unityNormals);
            mesh.SetUVs(0, unityUVs);

            // Multi-material: each material gets its own submesh so
            // MeshRenderer.sharedMaterials[i] maps to submesh i
            int activeSubmeshCount = submeshTriangles.Count(s => s.Count > 0);
            if (activeSubmeshCount <= 1)
            {
                mesh.SetTriangles(submeshTriangles.SelectMany(s => s).ToList(), 0);
            }
            else
            {
                mesh.subMeshCount = submeshCount;
                for (int i = 0; i < submeshCount; i++)
                    mesh.SetTriangles(submeshTriangles[i], i);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        #endregion

        #region Utility

        private static void ApplySurfaceTag(GameObject go, string surfaceTag)
        {
            // TODO: Proper implementation w/ physics groups
            {
                if (go.name.Contains("vert")) go.layer = PhysicsLayers.Vert;
                if (go.name.Contains("rail") || go.name.Contains("grind")) go.layer = PhysicsLayers.RailGrind;
                if (go.name.Contains("wallslide")) go.layer = PhysicsLayers.WallSlide;
                return;
            }
            int layer = LayerMask.NameToLayer(surfaceTag);
            if (layer >= 0) go.layer = layer;
            else Debug.LogWarning($"[VmapImporter] Surface layer '{surfaceTag}' not defined in Tags and Layers settings.");
        }

        #endregion

        #region Data Helpers

        /// <summary>
        /// Safely converts a Datamodel IntArray to int[].
        /// Iterates via foreach because Datamodel.Array's non-generic IList indexer
        /// throws NotImplementedException.
        /// </summary>
        private static int[] ToIntArray(IntArray source)
        {
            if (source == null || source.Count == 0) return Array.Empty<int>();
            var result = new int[source.Count];
            int i = 0;
            foreach (int val in source) result[i++] = val;
            return result;
        }

        /// <summary>
        /// Resolves per-face material indices from the FaceData "materialindex" stream.
        ///
        /// Indirection chain:
        ///   face f → FaceDataIndices[f] → FaceData "materialindex" stream[dataIdx] → material index
        /// The material index then looks up into CDmePolygonMesh.Materials[].
        /// </summary>
        private static int[] GetFaceMaterialIndices(CDmePolygonMeshDataArray faceData, int[] faceDataIndices)
        {
            if (faceData == null || faceDataIndices.Length == 0)
                return Array.Empty<int>();

            int[] materialIndexStream = null;
            foreach (Element streamElem in faceData.Streams)
            {
                if (streamElem is CDmePolygonMeshDataStream stream && stream.StandardAttributeName == "materialindex")
                {
                    if (streamElem.ContainsKey("data"))
                        materialIndexStream = ToIntArrayFromEnumerable(streamElem["data"] as System.Collections.IEnumerable);
                    break;
                }
            }

            if (materialIndexStream == null)
                return new int[faceDataIndices.Length]; // All zeros = single material

            var result = new int[faceDataIndices.Length];
            for (int f = 0; f < faceDataIndices.Length; f++)
            {
                int dataIdx = faceDataIndices[f];
                result[f] = (dataIdx >= 0 && dataIdx < materialIndexStream.Length) ? materialIndexStream[dataIdx] : 0;
            }
            return result;
        }

        private static int[] ToIntArrayFromEnumerable(System.Collections.IEnumerable source)
        {
            if (source == null) return Array.Empty<int>();
            var result = new List<int>();
            foreach (var item in source) result.Add(Convert.ToInt32(item));
            return result.ToArray();
        }

        /// <summary> Extracts a Vector3 data stream by standardAttributeName (e.g. "position", "normal"). </summary>
        private static List<SysVector3> GetStreamVector3(CDmePolygonMeshDataArray dataArray, string attributeName)
        {
            if (dataArray == null) return null;
            foreach (Element streamElem in dataArray.Streams)
            {
                if (streamElem is CDmePolygonMeshDataStream stream && stream.StandardAttributeName == attributeName)
                {
                    if (!streamElem.ContainsKey("data")) continue;
                    var result = new List<SysVector3>();
                    if (streamElem["data"] is System.Collections.IEnumerable enumerable)
                        foreach (var item in enumerable)
                            if (item is SysVector3 v) result.Add(v);
                    return result;
                }
            }
            return null;
        }

        /// <summary> Extracts a Vector2 data stream by standardAttributeName (e.g. "texcoord"). </summary>
        private static List<SysVector2> GetStreamVector2(CDmePolygonMeshDataArray dataArray, string attributeName)
        {
            if (dataArray == null) return null;
            foreach (Element streamElem in dataArray.Streams)
            {
                if (streamElem is CDmePolygonMeshDataStream stream && stream.StandardAttributeName == attributeName)
                {
                    if (!streamElem.ContainsKey("data")) continue;
                    var result = new List<SysVector2>();
                    if (streamElem["data"] is System.Collections.IEnumerable enumerable)
                        foreach (var item in enumerable)
                            if (item is SysVector2 v) result.Add(v);
                    return result;
                }
            }
            return null;
        }

        #endregion
    }
}
