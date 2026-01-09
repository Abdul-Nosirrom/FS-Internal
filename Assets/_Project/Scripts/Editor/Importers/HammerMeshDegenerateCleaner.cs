using System;
using System.Collections.Generic;
using Assimp;
using UnityEditor;
using UnityEngine;
using Mesh = UnityEngine.Mesh;
using Object = System.Object;

namespace FS.Editor
{
    /// <summary>
    /// Process incoming hammer meshes to clean degenerate meshes, ensuring same meshes refer to the same asset as opposed to having one-per-object as is
    /// usual with hammer imports
    /// </summary>
    public class HammerMeshDegenerateCleaner : AssetPostprocessor
    {
        //public override int GetPostprocessOrder() => 0; // Run before other processors, most importantly is before mesh colliders

        private Dictionary<string, Assimp.Mesh> m_storedMeshes = new();
        private Dictionary<string, string> m_meshRemap = new();
        
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("hmr")) return; // Hammer file processing only
            
            AssimpContext importer = new();
            var scene = importer.ImportFile(assetPath);
            
            if (scene == null) return;

            foreach (var mesh in scene.Meshes)
            {
                // Check equality against stored meshes
                bool foundEqual = false;

                var meshName = GetMeshName(mesh);
                
                foreach (var storedMesh in m_storedMeshes.Values)
                {
                    var storedMeshName = GetMeshName(storedMesh);
                    if (AreMeshesEqual(mesh, storedMesh))
                    {
                        Debug.Log($"[Mesh Degeneracy] Found equal mesh {meshName} to stored mesh {storedMeshName} from {assetPath}");
                        m_meshRemap[meshName] = storedMeshName;
                        foundEqual = true;
                        break;
                    }
                }
                if (!foundEqual)
                {
                    Debug.Log($"[Mesh Degeneracy] Storing new unique mesh {meshName} from {assetPath}");
                    m_storedMeshes[meshName] = mesh;
                }
            }
            
            Debug.Log($"[Mesh Degeneracy] Processing {assetPath} for degenerate meshes, found {m_meshRemap.Count} remaps");
        }

        private void OnPostprocessModel(GameObject g)
        {
            foreach (var child in g.transform)
            {
                var childMeshFilter = ((Transform)child).GetComponent<MeshFilter>();
                if (childMeshFilter == null) continue;
                var mesh = childMeshFilter.sharedMesh;
                if (mesh == null) continue;

                // First check if its the player-starting mesh, if so skip it
                if (childMeshFilter.gameObject.name.Contains("player_start"))
                {
                    Debug.Log($"[Mesh Degeneracy] Skipping player_start mesh {mesh.name} in {g.name}");
                    var material = childMeshFilter.GetComponent<MeshRenderer>()?.sharedMaterial;
                    if (material != null)
                        UnityEngine.Object.DestroyImmediate(material, true);
                    UnityEngine.Object.DestroyImmediate(childMeshFilter.sharedMesh, true);
                    UnityEngine.Object.DestroyImmediate(childMeshFilter.gameObject);
                    continue;
                }
                
                // Are we storing this mesh data already? If so delete this one and let the mesh filter point to the existing one
                if (!m_meshRemap.TryGetValue(childMeshFilter.sharedMesh.name, out var remappedMesh))
                {
                    Debug.Log($"[Mesh Degeneracy] No remap found for mesh {childMeshFilter.sharedMesh.name} in {g.name}");
                    continue;
                }
                
                // Get the existing mesh, delete the current and assign the mesh filter to the existing one
                var ogMeshChild = g.transform.Find(remappedMesh);
                if (ogMeshChild == null) continue;
                var ogMeshFilter = ogMeshChild.GetComponent<MeshFilter>();
                if (ogMeshFilter == null) continue;
                
                Debug.Log($"[Mesh Degeneracy] Remapping mesh {childMeshFilter.sharedMesh.name} to {remappedMesh} in {g.name}");
                UnityEngine.Object.DestroyImmediate(childMeshFilter.sharedMesh, true);
                childMeshFilter.sharedMesh = ogMeshFilter.sharedMesh;
                
                // Does it have a mesh collider? If so reassign that too
                var childMeshCollider = ((Transform)child).GetComponent<MeshCollider>();
                if (childMeshCollider != null)
                {
                    childMeshCollider.sharedMesh = ogMeshFilter.sharedMesh;
                }
            }
        }

        private string GetMeshName(Assimp.Mesh mesh)
        {
            var meshName = mesh.Name;
            if (meshName.EndsWith("Mesh"))
                meshName = mesh.Name.Substring(0, mesh.Name.Length - 4); // Remove "Mesh
            return meshName;
        }
        
        private bool AreMeshesEqual(Assimp.Mesh a, Assimp.Mesh b)
        {
            if (a.VertexCount != b.VertexCount) return false;

            var aVertices = a.Vertices;
            var bVertices = b.Vertices;
            
            var aNormals = a.Normals;
            var bNormals = b.Normals;
            
            var aHasUVs = a.HasTextureCoords(0);
            var bHasUVs = b.HasTextureCoords(0);

            if (aHasUVs != bHasUVs)
            {
                Debug.Log($"[Mesh Degeneracy] UV presence mismatch: {aHasUVs} != {bHasUVs}");
                return false;
            }
            
            var aUVs = aHasUVs ?  a.TextureCoordinateChannels[0] : null;
            var bUVs = bHasUVs ? b.TextureCoordinateChannels[0] : null;
            
            var aHasColors = a.HasVertexColors(0);
            var bHasColors = b.HasVertexColors(0);

            if (aHasColors != bHasColors)
            {
                Debug.Log($"[Mesh Degeneracy] Vertex color presence mismatch: {aHasColors} != {bHasColors}");
                return false;
            }
            
            var aColors = aHasColors ? a.VertexColorChannels[0] : null;
            var bColors = bHasColors ? b.VertexColorChannels[0] : null;

            for (int i = 0; i < a.VertexCount; i++)
            {
                if (aVertices[i] != bVertices[i])
                {
                    //Debug.Log($"[Mesh Degeneracy] Vertex mismatch at {i} : {aVertices[i]} != {bVertices[i]}");
                    return false;
                }
                if (aNormals[i] != bNormals[i])
                {
                    //Debug.Log($"[Mesh Degeneracy] Normal mismatch at {i} : {aNormals[i]} != {bNormals[i]}");
                    return false;
                }
                if (aHasUVs && aUVs[i] != bUVs[i])
                {
                    //Debug.Log($"[Mesh Degeneracy] UV mismatch at {i} : {aUVs[i]} != {bUVs[i]}");
                    return false;
                }
                if (aHasColors && aColors[i] != bColors[i])
                {
                    //Debug.Log($"[Mesh Degeneracy] Vertex color mismatch at {i} : {aColors[i]} != {bColors[i]}");
                    return false;
                }
            }

            var aTriangles = a.Faces;
            var bTriangles = b.Faces;
            if (aTriangles.Count != bTriangles.Count) return false;
            for (int i = 0; i < aTriangles.Count; i++)
            {
                if (aTriangles[i].IndexCount != bTriangles[i].IndexCount)
                {
                    Debug.Log($"[Mesh Degeneracy] Face index count mismatch at {i} : {aTriangles[i].IndexCount} != {bTriangles[i].IndexCount}");
                    return false;
                }

                if (aTriangles[i].Indices.Equals(bTriangles[i].Indices))
                {
                    Debug.Log($"[Mesh Degeneracy] Face indices mismatch at {i} : {string.Join(",", aTriangles[i].Indices)} != {string.Join(",", bTriangles[i].Indices)}");
                    return false;
                }
            }

            return true;
        }
    }
}