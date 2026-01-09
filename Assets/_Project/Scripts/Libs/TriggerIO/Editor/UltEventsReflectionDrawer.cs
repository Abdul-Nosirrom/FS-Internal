using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FS.Interaction.Editor
{
    public static class UltEventsReflectionDrawer
    {
        private static Type _persistentCallDrawerType;
        private static Type _drawerStateType;
        
        private static MethodInfo _doMemberFieldGUIMethod;
        private static MethodInfo _doTargetFieldGUIMethod;
        private static MethodInfo _drawMethodArgumentsMethod;
        private static MethodInfo _drawFieldArgumentMethod;
        
        private static MethodInfo _beginCallMethod;
        private static MethodInfo _endCallMethod;
        private static FieldInfo _drawerStateCurrentProperty;
        
        static UltEventsReflectionDrawer()
        {
            // Get the types
            _persistentCallDrawerType = Type.GetType("UltEvents.Editor.PersistentCallDrawer, Kybernetik.UltEvents.Editor");
            _drawerStateType = Type.GetType("UltEvents.Editor.DrawerState, Kybernetik.UltEvents.Editor");
            
            if (_persistentCallDrawerType == null || _drawerStateType == null)
            {
                Debug.LogError("Failed to find UltEvents types. Check assembly name.");
                return;
            }
            
            // Store the property itself, not the value (in case it changes)
            _drawerStateCurrentProperty = _drawerStateType.GetField(
                "Current", 
                BindingFlags.Public | BindingFlags.Static);
            
            if (_drawerStateCurrentProperty == null)
            {
                Debug.LogError("Failed to find DrawerState.Current property");
            }
            
            // Get methods from PersistentCallDrawer
            _doMemberFieldGUIMethod = _persistentCallDrawerType.GetMethod(
                "DoMemberFieldGUI", 
                BindingFlags.NonPublic | BindingFlags.Static);
                
            _doTargetFieldGUIMethod = _persistentCallDrawerType.GetMethod(
                "DoTargetFieldGUI", 
                BindingFlags.NonPublic | BindingFlags.Static);
                
            _drawMethodArgumentsMethod = _persistentCallDrawerType.GetMethod(
                "DrawMethodArguments", 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            _drawFieldArgumentMethod = _persistentCallDrawerType.GetMethod(
                "DrawFieldArgument", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Get methods from DrawerState
            _beginCallMethod = _drawerStateType.GetMethod(
                "BeginCall", 
                BindingFlags.Public | BindingFlags.Instance);
                
            _endCallMethod = _drawerStateType.GetMethod(
                "EndCall", 
                BindingFlags.Public | BindingFlags.Instance);
                
            if (_beginCallMethod == null)
                Debug.LogError("Failed to find BeginCall method");
            if (_endCallMethod == null)
                Debug.LogError("Failed to find EndCall method");
        }
        
        // Helper to get the current DrawerState instance
        private static object GetDrawerStateCurrent()
        {
            if (_drawerStateCurrentProperty == null)
            {
                Debug.LogError("DrawerState.Current property not found");
                return null;
            }
            
            var current = _drawerStateCurrentProperty.GetValue(null);
            if (current == null)
            {
                Debug.LogError("DrawerState.Current returned null");
            }
            
            return current;
        }
        
        // Call DoMemberFieldGUI (static method)
        public static void DoMemberFieldGUI(Rect area, MemberInfo member, bool autoOpenMethodMenu)
        {
            _doMemberFieldGUIMethod?.Invoke(null, new object[] { area, member, autoOpenMethodMenu });
        }
        
        // Call DoTargetFieldGUI (static method)
        public static void DoTargetFieldGUI(Rect area, SerializedProperty targetProperty, 
            SerializedProperty memberNameProperty, out bool autoOpenMemberMenu)
        {
            object[] parameters = new object[] { area, targetProperty, memberNameProperty, false };
            _doTargetFieldGUIMethod?.Invoke(null, parameters);
            autoOpenMemberMenu = (bool)parameters[3]; // Get the out parameter
        }
        
        // Call DrawMethodArguments (instance method - needs drawer instance)
        public static void DrawMethodArguments(object drawerInstance, Rect area, MethodBase method)
        {
            _drawMethodArgumentsMethod?.Invoke(drawerInstance, new object[] { area, method });
        }
        
        // Call DrawFieldArgument (instance method - needs drawer instance)
        public static void DrawFieldArgument(object drawerInstance, Rect area, FieldInfo field, bool isGetter)
        {
            _drawFieldArgumentMethod?.Invoke(drawerInstance, new object[] { area, field, isGetter });
        }
        
        // Call DrawerState.Current.BeginCall
        public static void BeginCall(SerializedProperty callProperty)
        {
            var current = GetDrawerStateCurrent();
            if (current == null)
                return;
                
            _beginCallMethod?.Invoke(current, new object[] { callProperty });
        }
        
        // Call DrawerState.Current.EndCall
        public static void EndCall()
        {
            var current = GetDrawerStateCurrent();
            if (current == null)
                return;
                
            _endCallMethod?.Invoke(current, null);
        }
        
        // Helper to create a PersistentCallDrawer instance if needed for instance methods
        public static object CreateDrawerInstance()
        {
            return Activator.CreateInstance(_persistentCallDrawerType, true);
        }
    }
}