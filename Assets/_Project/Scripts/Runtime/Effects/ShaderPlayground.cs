// using FS.Rendering;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.RenderGraphModule;
// using UnityEngine.Rendering.Universal;
//
// public class ShaderPlayground : ICameraRenderPass
// {
//     private static ShaderPlayground instance;
//     private static ShaderPlayground Instance
//     {
//         get { return instance ??= new ShaderPlayground(); }
//     }
//     
//     [RuntimeInitializeOnLoadMethod]
//     [InitializeOnLoadMethod]
//     private static void Register()
//     {
//         
//         Instance.RemoveGlobalCommandBuffer();
//         Instance.AddGlobalCommandBuffer(RenderPassEvent.AfterRenderingSkybox);
//     }
//
//     private static Material material;
//
//     public ShaderPlayground()
//     {
//         if (material != null) Object.DestroyImmediate(material);
//         material = new Material(Shader.Find("Hidden/Noise")) { hideFlags = HideFlags.HideAndDontSave };
//     }
//     
//     public void OnCameraRender(CommandBuffer cmd, TextureHandle source, TextureHandle dest)
//     {
//         Blitter.BlitCameraTexture(cmd, source, dest, material, 0);
//     }
// }