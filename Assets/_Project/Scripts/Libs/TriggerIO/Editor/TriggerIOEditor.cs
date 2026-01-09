using System;
using System.Collections.Generic;
using System.Reflection;
using FS.Editor;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UltEvents;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Events;

namespace FS.Interaction.Editor
{
    [CustomEditor(typeof(FS.Interaction.TriggerIO))]
    public class TriggerIOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Trigger IO Window"))
            {
                TriggerIOEditorWindow.ShowWindow();
            }
        }
    }
    
    public class TriggerIOEditorWindow : EditorWindow
    {
        [MenuItem("Free Skies/Trigger IO Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<TriggerIOEditorWindow>("Trigger IO");
            window.titleContent.image = EditorIcons.LightBulb.Raw;
            window.maximized = false;
            window.position = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 800, 600);
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private GameObject m_targetObject;
        private TriggerIO m_triggerObject;

        private enum Tabs
        {
            Outputs,
            Inputs
        }
        private Tabs m_currentTab = Tabs.Outputs;

        private List<TriggerIO.TriggerAction> m_inputActions = new List<TriggerIO.TriggerAction>();

        private SerializedProperty[] m_outputPersistentCalls;
        private SerializedProperty[] m_inputPersistentCalls;

        private TableView<TriggerIO.TriggerAction> m_outputTableView;
        private TableView<TriggerIO.TriggerAction> m_inputTableView;

        private int m_selectedPersistentCall;

        private void OnEnable()
        {
            InitializeData();

            m_outputTableView = new();
            m_inputTableView = new();
            
            // Setup output columns
            {
                m_outputTableView.AddColumn("", 32, DrawPaddingIcon).SetMaxWidth(32);
                m_outputTableView.AddColumn("Event", 100, DrawEventTrigger);
                m_outputTableView.AddColumn("Target", 100, DrawEventTargetSelector);
                m_outputTableView.AddColumn("Action", 100, DrawEventActionSelector);
                m_outputTableView.AddColumn("Parameters", 100, DrawEventActionParameters);
                m_outputTableView.AddColumn("Delay", 64, DrawEventDelayField).SetMaxWidth(128);
                m_outputTableView.AddColumn("Once?", 48, DrawEventOnceToggle).SetMaxWidth(48);
                m_outputTableView.AddColumn("", 32, DrawDeleteField).SetMaxWidth(32);
            }
            
            // Setup input columns
            {
                m_inputTableView.AddColumn("", 32, DrawPaddingIcon).SetMaxWidth(32);
                m_inputTableView.AddColumn("Caller", 100, DrawInputEventCaller);
                m_inputTableView.AddColumn("Event", 100, DrawEventTrigger);
                m_inputTableView.AddColumn("Action", 100, DrawEventActionSelector);
                m_inputTableView.AddColumn("Parameters", 100, DrawEventActionParameters);
                m_inputTableView.AddColumn("Delay", 64, DrawEventDelayField).SetMaxWidth(128);
                m_inputTableView.AddColumn("Once?", 48, DrawEventOnceToggle).SetMaxWidth(48);
                m_inputTableView.AddColumn("", 32, DrawInspectCaller).SetMaxWidth(32);
            }

            EditorApplication.update += ConditionalRepaint;
            AssemblyReloadEvents.afterAssemblyReload += InitializeData;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ConditionalRepaint;
            AssemblyReloadEvents.afterAssemblyReload -= InitializeData;
        }

        private void ConditionalRepaint()
        {
            if (Time.frameCount % 20 == 0) Repaint();
        }

        private void InitializeData()
        {
            FindSelection();
            RefreshSerializedPropertyCache();
        }
        
        private void RefreshSerializedPropertyCache()
        {
            FindInputs();

            m_selectedPersistentCall = -1;

            // Just a placeholder for now, we can use this later if we want to do more complex property drawing
            if (m_triggerObject != null)
            {
                m_outputPersistentCalls = new SerializedProperty[m_triggerObject.m_actions.Count];
                var so = new SerializedObject(m_triggerObject);
                for (int i = 0; i < m_triggerObject.m_actions.Count; i++)
                {
                    var actionsProp = so.FindProperty("m_actions");
                    var actionProp = actionsProp.GetArrayElementAtIndex(i);
                    m_outputPersistentCalls[i] = actionProp.FindPropertyRelative("m_event")
                        .FindPropertyRelative("_PersistentCalls").GetArrayElementAtIndex(0);
                }
            }

            m_inputPersistentCalls = new SerializedProperty[m_inputActions.Count];
            for (int i = 0; i < m_inputActions.Count; i++)
            {
                var action = m_inputActions[i];
                var so = new SerializedObject(action.m_owner);
                var actionsProp = so.FindProperty("m_actions");
                // Find the index of this action in the owner's actions
                int actionIndex = action.m_owner.m_actions.IndexOf(action);
                if (actionIndex >= 0)
                {
                    var actionProp = actionsProp.GetArrayElementAtIndex(actionIndex);
                    m_inputPersistentCalls[i] = actionProp.FindPropertyRelative("m_event")
                        .FindPropertyRelative("_PersistentCalls").GetArrayElementAtIndex(0);
                }
            }
        }

        private void FindInputs()
        {
            // Update inputs
            m_inputActions.Clear();
            var triggerIOs = GameObject.FindObjectsByType<TriggerIO>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var triggerIO in triggerIOs)
            {
                foreach (var action in triggerIO.m_actions)
                {
                    if (!action.m_event.HasCalls) continue;

                    GameObject targetObj = null;
                        
                    if (action.m_event.PersistentCallsList[0].Target is GameObject go)
                        targetObj = go;
                    else if (action.m_event.PersistentCallsList[0].Target is Component comp)
                        targetObj = comp.gameObject;

                    if (targetObj == m_targetObject)
                    {
                        m_inputActions.Add(action);
                    }
                }
            }
        }

        private void FindSelection()
        {
            var selectedGO = Selection.activeGameObject;
            if (selectedGO == null) return;

            bool shouldRefresh = selectedGO != m_targetObject;
            
            m_targetObject = selectedGO;
            m_triggerObject = selectedGO.GetComponent<TriggerIO>();
            if (shouldRefresh)
            {
                RefreshSerializedPropertyCache();
            }
        }

        private void OnGUI()
        {
            // Keep window icon updated
            titleContent.image = EditorIcons.LightBulb.Raw;
            
            FindSelection();
            
            if (m_targetObject == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject to view Trigger IO info", MessageType.Info);
                return;
            }

            if (m_triggerObject == null) m_currentTab = Tabs.Inputs;
            
            // Grid - left column is info [Name / Outputs () / Inputs () / Add Event]
            EditorGUILayout.BeginHorizontal();

            // Main Column
            {
                var tabRect = new Rect(0, 0, 216, position.height);
                EditorGUI.DrawRect(tabRect, new Color(0, 0, 0, 0.3f));
                
                EditorGUILayout.BeginVertical(GUIStyles.GUIStyles.HelpBox, GUILayout.Width(128), GUILayout.ExpandWidth(false));
                FreeSkiesEditor.DrawLogo(100f);

                var labelRect = EditorGUILayout.GetControlRect(GUILayout.Height(32));
                bool isHoveringLabel = labelRect.Contains(Event.current.mousePosition);
                float colorMultiplier = isHoveringLabel ? 1.4f : 1f;
                SirenixEditorGUI.DrawRoundRect(labelRect, colorMultiplier * new Color(0.1f, 0.1f, 0.1f, 1), 6);
                EditorGUI.DropShadowLabel(labelRect, $"{m_targetObject.name}", SirenixGUIStyles.BoldTitleCentered);
                
                if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && isHoveringLabel)
                {
                    Selection.activeGameObject = m_targetObject;
                    SceneView.FrameLastActiveSceneView();
                    Event.current.Use();
                }

                var ogTab = m_currentTab;
                var ogColor = GUI.backgroundColor;
                foreach (var tabID in System.Enum.GetValues(typeof(Tabs)))
                {
                    var tab = (Tabs) tabID;
                    
                    GUI.backgroundColor = m_currentTab == tab ? Color.deepSkyBlue : ogColor;
                    GUI.enabled = !(tab == Tabs.Outputs && m_triggerObject == null);
                    
                    int numEvents = tab == Tabs.Inputs ? m_inputActions.Count : (m_triggerObject?.m_actions.Count ?? 0);
                    if (GUILayout.Button($"{tab} ({numEvents})", GUILayout.Height(64)))
                        m_currentTab = tab;
                    
                    GUI.enabled = true;
                }
                GUI.backgroundColor = ogColor;
                
                // Tab change - reset data
                if (ogTab != m_currentTab)
                {
                    m_selectedPersistentCall = -1;
                }
                
                GUILayout.FlexibleSpace();
            
                GUI.enabled = m_triggerObject != null;
                if (SirenixEditorGUI.SDFIconButton("Add Event", 42, SdfIconType.Plus))
                {
                    if (m_triggerObject != null)
                    {
                        using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);
                        var newEvt = new TriggerIO.TriggerAction();
                        m_triggerObject.AddEventAction(newEvt);
                        RefreshSerializedPropertyCache();
                    }
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndVertical();
            }

            int callInspectorHeight = m_selectedPersistentCall < 0 ? 0 : 128;
            
            m_callIndex = 0;
            var tableRect = new Rect(216, 0, position.width - 216, position.height - callInspectorHeight);
            
            GUILayout.BeginArea(tableRect, GUIStyles.GUIStyles.HelpBox);

            if (m_currentTab == Tabs.Outputs)
            {
                m_outputTableView.DrawTableGUI(m_triggerObject.m_actions.ToArray());
            }
            else if (m_currentTab == Tabs.Inputs)
            {
                GUI.enabled = false;
                m_inputTableView.DrawTableGUI(m_inputActions.ToArray());
                GUI.enabled = true;
            }

            GUILayout.EndArea();
            
            // // Inspector
            if (m_selectedPersistentCall >= 0)
            {
                var inspectorRect = new Rect(216, position.height - callInspectorHeight,
                    position.width - 216, callInspectorHeight);
                GUILayout.BeginArea(inspectorRect, GUIStyles.GUIStyles.HelpBox);
                
                EditorGUILayout.LabelField("Event Call Inspector", GUIStyles.GUIStyles.WhiteLargeCenterLabel);
                EditorGUILayout.Space(4);
                
                var persistentCallProp = m_currentTab == Tabs.Outputs ?
                    m_outputPersistentCalls[this.m_selectedPersistentCall] :
                    m_inputPersistentCalls[this.m_selectedPersistentCall];
                
                persistentCallProp.serializedObject.Update();
                EditorGUILayout.PropertyField(persistentCallProp, true);
                persistentCallProp.serializedObject.ApplyModifiedProperties();
                
                GUILayout.EndArea();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        
        // ------------------------- EVENT CALLBACKS FOR COLUMN/TABLE VIEW

        private int m_callIndex = 0;
        private void DrawPaddingIcon(Rect rect, TriggerIO.TriggerAction action)
        {
            float colorMultiplier = rect.Contains(Event.current.mousePosition) ? 1.5f : 1f;
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                m_selectedPersistentCall = m_callIndex;
                Event.current.Use();
                //Repaint();
            }

            if (m_selectedPersistentCall == m_callIndex)
                EditorGUI.DrawRect(rect, new Color(0.8f, 0.8f, 0.8f, 0.4f));
            
            var colorTint = m_currentTab == Tabs.Outputs ? Color.cornflowerBlue : Color.crimson;
            var ogColor = GUI.color;
            GUI.color = colorTint * colorMultiplier;
            GUI.DrawTexture(rect, EditorIcons.ArrowRight.Raw, ScaleMode.ScaleToFit);
            GUI.color = ogColor;
            
            // Set the drawer state for our current event to be drawn
            var eventProp = m_currentTab == Tabs.Outputs ?
                m_outputPersistentCalls[m_callIndex] :
                m_inputPersistentCalls[m_callIndex];
            
            // Closing call is done for the last column based on input/output tab selection
            UltEventsReflectionDrawer.BeginCall(eventProp); 
            
            m_callIndex++;
        }

        private void DrawEventTrigger(Rect rect, TriggerIO.TriggerAction action)
        {
            rect.width -= 32;
            action.m_trigger = (TriggerIO.TriggerType)EditorGUI.EnumPopup(rect, action.m_trigger);
            rect.x += rect.width;
            rect.width = 32;
            GUI.DrawTexture(rect, EditorIcons.ArrowRight.Raw, ScaleMode.ScaleToFit);
        }

        private void DrawEventTargetSelector(Rect rect, TriggerIO.TriggerAction action)
        {
            using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);

            rect.width -= 32;
            
            var persistenCallProp = m_currentTab == Tabs.Outputs ?
                m_outputPersistentCalls[m_callIndex-1] :
                m_inputPersistentCalls[m_callIndex-1];
            var targetProp = persistenCallProp.FindPropertyRelative("_Target");

            var targetObj = targetProp.objectReferenceValue;
            targetObj = SirenixEditorFields.UnityObjectField(rect, targetObj, typeof(UnityEngine.Object), true);
            
            // We want to set the target on the first persistent call
            {
                persistenCallProp.serializedObject.Update();
                targetProp.objectReferenceValue = targetObj;
                if (persistenCallProp.serializedObject.ApplyModifiedProperties())
                    RefreshSerializedPropertyCache();
            }
        }

        private void DrawEventActionSelector(Rect rect, TriggerIO.TriggerAction action)
        {
            if (m_triggerObject) Undo.RecordObject(m_triggerObject, "Modify Trigger IO Event Action");

            var member = action.m_event.PersistentCallsList[0].Member;
            var memberID = member == null ? -1 : member.GetHashCode();
            UltEventsReflectionDrawer.DoMemberFieldGUI(rect, member, false);

            var newMember = member == null ? -1 : member.GetHashCode();
            if (memberID != newMember)
            {
                EditorUtility.SetDirty(m_triggerObject);
            }
        }

        private void DrawEventActionParameters(Rect rect, TriggerIO.TriggerAction action)
        {
            using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);
            
            // Padding
            rect.x += 12;
            rect.width -= 24;
            
            var persistentCallProp = m_currentTab == Tabs.Outputs ?
                m_outputPersistentCalls[m_callIndex-1] :
                m_inputPersistentCalls[m_callIndex-1];

            var argumentsArray = persistentCallProp.FindPropertyRelative("_PersistentArguments");

            if (argumentsArray.arraySize > 0)
            {
                var argProp = argumentsArray.GetArrayElementAtIndex(0);
                persistentCallProp.serializedObject.Update();
                EditorGUI.PropertyField(rect, argProp, GUIContent.none, false);
                persistentCallProp.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorGUI.DropShadowLabel(rect, "No Parameters");
            }
        }
        
        private void DrawEventDelayField(Rect rect, TriggerIO.TriggerAction action)
        {
            using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);
            rect.width -= 24;
            action.m_delay = EditorGUI.FloatField(rect, action.m_delay);
        }
        
        private void DrawEventOnceToggle(Rect rect, TriggerIO.TriggerAction action)
        {
            using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);
            action.m_once = EditorGUI.Toggle(rect, action.m_once);
        }
        
        private void DrawDeleteField(Rect rect, TriggerIO.TriggerAction action)
        {
            using var dirtyScope = new DirtyIfChangedScope(m_triggerObject);
            // Only for outputs
            if (SirenixEditorGUI.SDFIconButton(rect, "", SdfIconType.Trash))
            {
                m_triggerObject.RemoveEventAction(action);
                RefreshSerializedPropertyCache();
            }
            
            UltEventsReflectionDrawer.EndCall();
        }

        private void DrawInputEventCaller(Rect rect, TriggerIO.TriggerAction action)
        {
            // Only for inputs
            EditorGUI.DropShadowLabel(rect, $"{action?.m_owner.gameObject.name ?? "BROKEN"}");
        }

        private void DrawInspectCaller(Rect rect, TriggerIO.TriggerAction action)
        {
            var enabledState = GUI.enabled;
            GUI.enabled = true;
            // Only for inputs
            if (SirenixEditorGUI.SDFIconButton(rect, "", SdfIconType.Eye))
            {
                Selection.activeGameObject = action.m_owner.gameObject;
                SceneView.FrameLastActiveSceneView(); // To focus the scene view camera
            }
            
            UltEventsReflectionDrawer.EndCall();
            GUI.enabled = enabledState;
        }
    }
}