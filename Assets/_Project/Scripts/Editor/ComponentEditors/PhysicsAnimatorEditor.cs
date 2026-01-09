using FS.UI.Editor;
using UnityEditor;

namespace FS.Editor
{
    [CustomEditor(typeof(PhysicsAnimator))]
    public class PhysicsAnimatorEditor : TweenAnimationEditor
    {
        private SerializedProperty m_playOnStartProp;
        private SerializedProperty m_pingPongProp;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            m_playOnStartProp ??= serializedObject.FindProperty("m_playOnStart");
            m_pingPongProp ??= serializedObject.FindProperty("m_pingPong");
            
            bool playOnStart = m_playOnStartProp.boolValue;
            FreeSkiesEditor.ToggleButton("Play On Start", ref playOnStart);
            m_playOnStartProp.boolValue = playOnStart;
            bool pingPong = m_pingPongProp.boolValue;
            FreeSkiesEditor.ToggleButton("Ping Pong", ref pingPong);
            m_pingPongProp.boolValue = pingPong;

            serializedObject.ApplyModifiedProperties();
            
            base.OnInspectorGUI();
        }
    }
}