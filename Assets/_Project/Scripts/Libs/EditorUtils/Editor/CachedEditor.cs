using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FS.Editor
{
    /// <summary>[Editor-Only]
    /// A utility for manually drawing a <see cref="UnityEditor.Editor"/>.
    /// </summary>
    /// https://kybernetik.com.au/animancer/api/Animancer.Editor/CachedEditor
    public class CachedEditor : IDisposable
    {
        /************************************************************************************************************************/

        [NonSerialized] private Object[] _Targets = Array.Empty<Object>();
        [NonSerialized] private UnityEditor.Editor _Editor;
        [NonSerialized] private bool _WillCleanup;

        /************************************************************************************************************************/

        /// <summary>
        /// Creates a <see cref="UnityEditor.Editor"/> for the `target`
        /// and caches it to be returned by subsequent calls with the same `target`.
        /// </summary>
        public UnityEditor.Editor GetEditor(Object target, Type editorType = null)
        {
            if (_Targets.Length == 1 &&
                _Targets[0] == target &&
                _Editor != null)
                return _Editor;

            Dispose();

            SetArrayLength(1);
            _Targets[0] = target;
            _Editor = editorType != null 
                ? UnityEditor.Editor.CreateEditor(target, editorType) 
                : UnityEditor.Editor.CreateEditor(target);
            EnsureCleanup();
            return _Editor;
        }

        /// <summary>
        /// Creates a <see cref="UnityEditor.Editor"/> for the `targets`
        /// and caches it to be returned by subsequent calls with the same `targets`.
        /// </summary>
        public UnityEditor.Editor GetEditor(Object[] targets, Type editorType = null)
        {
            if (ContentsAreEqual(targets, _Targets) &&
                _Editor != null)
                return _Editor;

            Dispose();

            _Targets = targets;
            _Editor = editorType != null 
                ? UnityEditor.Editor.CreateEditor(targets, editorType) 
                : UnityEditor.Editor.CreateEditor(targets);
            EnsureCleanup();
            return _Editor;
        }

        /************************************************************************************************************************/

        /// <summary>Destroys the cached <see cref="UnityEditor.Editor"/>.</summary>
        public void Dispose()
            => Object.DestroyImmediate(_Editor);

        /************************************************************************************************************************/

        /// <summary>Ensures that <see cref="Dispose"/> will be called before assemblies are reloaded.</summary>
        private void EnsureCleanup()
        {
            if (_WillCleanup)
                return;

            _WillCleanup = true;

            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        /************************************************************************************************************************/
        
        
        private void SetArrayLength(int length)
        {
            if (_Targets != null && _Targets.Length == length)
                return;

            _Targets = new Object[length];
        }
        
        /// <summary>Are both lists the same size with the same items in the same order?</summary>
        private bool ContentsAreEqual<T>(IList<T> a, IList<T> b)
        {
            if (a == null)
                return b == null;

            if (b == null)
                return false;

            var aCount = a.Count;
            var bCount = b.Count;
            if (aCount != bCount)
                return false;

            for (int i = 0; i < aCount; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;

            return true;
        }
    }
}