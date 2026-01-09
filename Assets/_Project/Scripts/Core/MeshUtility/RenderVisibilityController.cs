using System;
using System.Collections.Generic;
using System.Linq;
using FS.Animation;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages visibility of renderer groups with support for hierarchical relationships, flip-flop states, and mutually exclusive sets.
/// 
/// <para><b>Core Features:</b></para>
/// <list type="bullet">
/// <item>Organize renderers into named groups with optional fade transitions</item>
/// <item>Hierarchical parent-child relationships using dot notation (e.g., "Weapon.Holstered")</item>
/// <item>Binary flip-flop states using ! prefix (e.g., "State" and "!State")</item>
/// <item>Mutually exclusive sets where only one group can be visible at a time</item>
/// </list>
/// 
/// <para><b>Naming Conventions:</b></para>
/// <list type="bullet">
/// <item><b>Simple groups:</b> "FingerGuns", "BoxingGloves" - Independent visibility groups</item>
/// <item><b>Parent.Child:</b> "WhipArms.Chain" - Child automatically shows parent when shown</item>
/// <item><b>! prefix:</b> "State" and "!State" - Showing one automatically hides the other</item>
/// </list>
/// 
/// <para><b>Visibility Rules:</b></para>
/// <list type="bullet">
/// <item><b>Show("A.B"):</b> Shows B and implicitly shows parent A. Hides "!A.B" if exists.</item>
/// <item><b>Hide("A.B"):</b> Hides B only. Shows "!A.B" if exists. Parent A remains unchanged.</item>
/// <item><b>Show("A"):</b> Shows only A. Subgroups keep their current state. Hides "!A" if exists.</item>
/// <item><b>Hide("A"):</b> Hides A and all A.* subgroups. Shows "!A" if exists.</item>
/// <item><b>Exclusive sets:</b> Showing any group in a set automatically hides all others in that set.</item>
/// </list>
/// 
/// <para><b>Example Setup:</b></para>
/// <code>
/// Groups:
///   - WhipArms (wrist mesh - shared between states)
///   - WhipArms.Chain (equipped state)
///   - !WhipArms.Chain (holstered state)
/// 
/// Exclusive Set "Weapons":
///   - FingerGuns
///   - BoxingGloves
///   - WhipArms.Chain
///   - !WhipArms.Chain
/// 
/// Usage:
///   Show("WhipArms.Chain");     // Equips whip, shows wrist, hides other weapons
///   Show("!WhipArms.Chain");    // Holsters whip, shows wrist, hides other weapons
///   Show("BoxingGloves");       // Switches to gloves, hides all whip states
/// </code>
/// </summary>
public class RenderVisibilityController : MonoBehaviour
{
    private static readonly int VISIBILITY_SHADER_PROPERTY_ID = Shader.PropertyToID("_AlphaCutoff");

    [Serializable]
    public class VisibilityGroup
    {
        [TableColumnWidth(256, Resizable = false)]
        public string GroupName = "New Group";
        
        [ListDrawerSettings(
            ShowIndexLabels = false, 
            DraggableItems = true,
            ShowFoldout = true,
            ShowItemCount = false
        )]
        [ValueDropdown("@m_renderVisibilityController.gameObject.GetComponentsInChildren<UnityEngine.Renderer>()", IsUniqueList = true, DropdownTitle = "Select Renderers", ExcludeExistingValuesInList = true)]
        [PropertySpace(SpaceBefore = 5)]
        public List<Renderer> Renderers = new();

        [SerializeField, HideInInspector]
        public RenderVisibilityController m_renderVisibilityController;
        
        public bool IsHidden => m_targetVisibilityAlpha <= 0f;
        
        private float m_targetVisibilityAlpha;
        private float m_currentVisibilityAlpha;
        private Tween m_visibilityTween;
        
        public void SetVisibility(float target, bool immediate)
        {
            if (!Application.isPlaying) immediate = true; // to not manage materials being created
            m_targetVisibilityAlpha = target;
            if (immediate) SetVisibility_Internal(target);
            else
            {
                if (m_visibilityTween.isAlive) m_visibilityTween.Stop();
                float duration = Mathf.Abs(target - m_currentVisibilityAlpha) * 0.5f; // percentage so its always same speed
                m_visibilityTween = Tween.Custom(m_currentVisibilityAlpha, target, duration, SetVisibility_Internal);
            }
        }
        
        private void SetVisibility_Internal(float alpha)
        {
            m_currentVisibilityAlpha = alpha;
            
            foreach (var renderer in Renderers)
            {
                if (renderer != null)
                {
                    foreach (var mat in renderer.materials) mat.SetFloat(VISIBILITY_SHADER_PROPERTY_ID, 1-alpha);
                    renderer.enabled = alpha > 0f; // TODO: For later to disable invisible renderers, this line doesnt work as expected
                }
            }
        }
    }
    
    [Serializable]
    public class ExclusiveGroupSet
    {
        [Tooltip("Only one group from this list can be visible at a time")]
        [ValueDropdown("@m_renderVsibilityController.GetAllGroupNames()")]
        public List<string> GroupNames = new List<string>();
        [SerializeField, HideInInspector] public RenderVisibilityController m_renderVsibilityController;
    }
    
    [TableList]
    [SerializeField]
    private List<VisibilityGroup> m_visibilityGroups = new List<VisibilityGroup>();
    
    [InfoBox("Groups in the same set are mutually exclusive - showing one hides all others in the set.")]
    [ListDrawerSettings(ShowItemCount = false)]
    [SerializeField]
    private List<ExclusiveGroupSet> m_exclusiveGroupSets = new List<ExclusiveGroupSet>();
    
    #if UNITY_EDITOR
    private IEnumerable<string> GetAllGroupNames()
    {
        return m_visibilityGroups.Select(g => g.GroupName);
    }
    
    private void OnValidate()
    {
        // Ensure back-references are set
        foreach (var group in m_visibilityGroups) group.m_renderVisibilityController = this;
        foreach (var exclusiveSet in m_exclusiveGroupSets) exclusiveSet.m_renderVsibilityController = this;
    }
    #endif

    /// <summary>
    /// Gets the inverse group name by toggling the ! prefix.
    /// Examples: "WhipArms.Chain" -> "!WhipArms.Chain", "!WhipArms.Chain" -> "WhipArms.Chain"
    /// </summary>
    private string GetInverseGroupName(string groupName)
    {
        if (groupName.StartsWith("!"))
            return groupName.Substring(1); // Remove the !
        else
            return "!" + groupName; // Add the !
    }

    /// <summary>
    /// Gets a single group by exact name match.
    /// </summary>
    private bool TryGetVisibilityGroup(string groupName, out VisibilityGroup visibilityGroup)
    {
        foreach (var group in m_visibilityGroups)
        {
            if (group.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase))
            {
                visibilityGroup = group;
                return true;
            }
        }
        
        visibilityGroup = null;
        return false;
    }

    /// <summary>
    /// Gets all groups that match as children of the given parent.
    /// Example: "MainWeapon" returns "MainWeapon.Holstered", "MainWeapon.Equipped", "!MainWeapon.Holstered", etc.
    /// </summary>
    private IEnumerable<VisibilityGroup> GetChildGroups(string parentName)
    {
        // Strip ! prefix if present for parent matching
        string cleanParentName = parentName.StartsWith("!") ? parentName.Substring(1) : parentName;
    
        foreach (var group in m_visibilityGroups)
        {
            string cleanGroupName = group.GroupName.StartsWith("!") ? group.GroupName.Substring(1) : group.GroupName;
        
            if (cleanGroupName.StartsWith(cleanParentName + ".", StringComparison.OrdinalIgnoreCase))
            {
                yield return group;
            }
        }
    }
    
    private IEnumerable<VisibilityGroup> GetParentGroups(string childName)
    {
        // Strip ! prefix if present for child matching
        string cleanChildName = childName.StartsWith("!") ? childName.Substring(1) : childName;
        int lastDotIndex = cleanChildName.LastIndexOf('.');
        if (lastDotIndex < 0) yield break; // No parent exists

        string parentName = cleanChildName.Substring(0, lastDotIndex);
        
        foreach (var group in m_visibilityGroups)
        {
            string cleanGroupName = group.GroupName.StartsWith("!") ? group.GroupName.Substring(1) : group.GroupName;
        
            if (cleanGroupName.Equals(parentName, StringComparison.OrdinalIgnoreCase))
            {
                yield return group;
            }
        }
    }

    /// <summary>
    /// Finds the exclusive set that contains the given group name or its parent.
    /// For subgroups, checks both the subgroup itself and its parent hierarchy.
    /// Example: "C.A" will find exclusive sets containing either "C.A" or "C"
    /// </summary>
    private ExclusiveGroupSet FindExclusiveSet(string groupName)
    {
        // Strip ! prefix for matching
        string cleanGroupName = groupName.StartsWith("!") ? groupName.Substring(1) : groupName;
    
        foreach (var exclusiveSet in m_exclusiveGroupSets)
        {
            foreach (var setGroupName in exclusiveSet.GroupNames)
            {
                string cleanSetGroupName = setGroupName.StartsWith("!") ? setGroupName.Substring(1) : setGroupName;
            
                // Direct match
                if (cleanSetGroupName.Equals(cleanGroupName, StringComparison.OrdinalIgnoreCase))
                {
                    return exclusiveSet;
                }
            
                // Parent match - check if this subgroup belongs to a parent in the set
                // Example: "C.A" should match if "C" is in the set
                if (cleanGroupName.Contains("."))
                {
                    string parentName = cleanGroupName.Substring(0, cleanGroupName.LastIndexOf('.'));
                    if (cleanSetGroupName.Equals(parentName, StringComparison.OrdinalIgnoreCase))
                    {
                        return exclusiveSet;
                    }
                }
            }
        }
    
        return null;
    }

    /// <summary>
    /// Set visibility for a group or all subgroups.
    /// Rules:
    /// - Show("A.B") - Shows B and implicitly shows A if hidden, hides "!A.B" if it exists
    /// - Hide("A.B") - Hides B only, shows "!A.B" if it exists, A remains as-is
    /// - Show("A") - Shows only A (subgroups keep their current state), hides "!A" if it exists
    /// - Hide("A") - Hides A and all A.* subgroups, shows "!A" if it exists
    /// - Exclusive sets: Showing any group in a set automatically hides all others in that set
    /// </summary>
    public void SetVisibility(string groupName, bool visibility, bool immediate = false)
    {
        if (visibility) Show(groupName, immediate);
        else Hide(groupName, immediate);
    }
    
    public bool DoesVisibilityGroupExist(string groupName) => TryGetVisibilityGroup(groupName, out _);
    
    /// <summary>
    /// Show a group.
    /// - For subgroups (e.g., "A.B"): Shows the subgroup, hides "!A.B", and ensures parent "A" is visible
    /// - For parent groups (e.g., "A"): Shows the parent and hides "!A" (subgroups unchanged)
    /// - Handles exclusive sets by hiding conflicting groups AND their children
    /// </summary>
    public void Show(string groupName, bool immediate = false)
    {
        bool foundAny = false;
        
        if (TryGetVisibilityGroup(groupName, out var group))
        {
            foundAny = true;
            
            // Handle exclusive sets FIRST
            var exclusiveSet = FindExclusiveSet(groupName);
            if (exclusiveSet != null)
            {
                foreach (var otherGroupName in exclusiveSet.GroupNames)
                {
                    if (!otherGroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetVisibilityGroup(otherGroupName, out var otherGroup))
                        {
                            otherGroup.SetVisibility(0f, immediate);
                            
                            // Also hide all children of this group
                            foreach (var childGroup in GetChildGroups(otherGroupName)) childGroup.SetVisibility(0f, immediate);
                            
                            // Also hide the inverse of this group
                            string otherInverseGroupName = GetInverseGroupName(otherGroupName);
                            if (TryGetVisibilityGroup(otherInverseGroupName, out var otherInverseGroup)) otherInverseGroup.SetVisibility(0f, immediate);
                        }
                    }
                }
            }
            
            // Show the requested group
            group.SetVisibility(1f, immediate);
            
            // Hide the inverse group (works for both parents and children)
            string inverseGroupName = GetInverseGroupName(groupName);
            if (TryGetVisibilityGroup(inverseGroupName, out var inverseGroup)) inverseGroup.SetVisibility(0f, immediate);
            
            // If this is a subgroup, ensure parent is visible
            foreach (var parentGroup in GetParentGroups(groupName)) parentGroup.SetVisibility(1f, immediate);
        }
        
        #if UNITY_EDITOR
        if (!foundAny)
        {
            Debug.LogWarning($"[Render Visibility] No visibility group found matching '{groupName}' on {gameObject.name}", this);
        }
        #endif
    }

    /// <summary>
    /// Hide a group.
    /// - For subgroups (e.g., "A.B"): Hides the subgroup and shows "!A.B" (parent unchanged)
    /// - For parent groups (e.g., "A"): Hides parent, shows "!A", and hides ALL child groups (including inverses)
    /// </summary>
    public void Hide(string groupName, bool immediate = false)
    {
        bool foundAny = false;

        if (TryGetVisibilityGroup(groupName, out var group))
        {
            foundAny = true;
            
            // Hide the requested group
            group.SetVisibility(0f, immediate);

            // Check if any parent in the chain is hidden
            bool hasHiddenParent = false;
            foreach (var parentGroup in GetParentGroups(groupName))
            {
                if (parentGroup.IsHidden)
                {
                    hasHiddenParent = true;
                    break; // Early exit once we find one
                }
            }
            
            // Show the inverse group (works for both parents and children) [only if parents are not hidden]
            if (!hasHiddenParent)
            {
                string inverseGroupName = GetInverseGroupName(groupName);
                if (TryGetVisibilityGroup(inverseGroupName, out var inverseGroup))
                    inverseGroup.SetVisibility(1f, immediate);
            }

            // If this is a parent group, hide ALL children (including inverses)
            foreach (var childGroup in GetChildGroups(groupName)) childGroup.SetVisibility(0f, immediate);
        }

        #if UNITY_EDITOR
        if (!foundAny)
        {
            Debug.LogWarning($"[Render Visibility] No visibility group found matching '{groupName}' on {gameObject.name}", this);
        }
        #endif
    }

    /// <summary>
    /// Toggle the visibility on of the whole character mesh, unrelated to groups at all.
    /// For simplicity, this is handled by scaling the mesh back to its regular size
    /// </summary>
    public void ShowFullMesh(float duration = 0)
    {
        Tween.Scale(transform, Vector3.one, duration, Ease.OutQuad);
    }

    /// <summary>
    /// Hide the whole mesh, simply done by scaling it to 0
    /// </summary>
    public void HideFullMesh(float duration = 0)
    {
        Tween.Scale(transform, Vector3.one * 0f, duration, Ease.InQuad);
    }
}

[EventPath("Render/Set Render Visibility")]
[Serializable]
public class VisibilityAnimationEvent : IAnimationEvent
{
    public string Name => VisibilityGroupName == null 
        ? "SetRenderVisibility" 
        : (IsRangedEvent 
            ? $"Visibility ({VisibilityGroupName})" 
            : $"Visibility ({VisibilityGroupName} = {Visiblility})");
    
    public string VisibilityGroupName = null;
    public bool IsRangedEvent => IsVisibilityControlledByRange;
    
    public bool IsVisibilityControlledByRange;
    
    [HideIf("IsVisibilityControlledByRange")] 
    public bool Visiblility = true;
    
    private Dictionary<GameObject, RenderVisibilityController> m_renderVisibilityCache = new();

    private RenderVisibilityController TryGetVisibilityController(GameObject context)
    {
        return context.GetComponent<RenderVisibilityController>();
        if (!m_renderVisibilityCache.TryGetValue(context, out var controller))
        {
            controller = context.GetComponent<RenderVisibilityController>();
            m_renderVisibilityCache[context] = controller;
        }

        return controller;
    }

    private void ToggleVisibility(GameObject context, bool visibility)
    {
        var controller = TryGetVisibilityController(context);
        if (controller) controller.SetVisibility(VisibilityGroupName, visibility);
    }

    // Either we're a range event in which we set it to true on start and false on end,
    public void Start(GameObject context) => ToggleVisibility(context, true);
    public void End(GameObject context) => ToggleVisibility(context, false);
    
    // Or we're a single point event in which we set it based on the Visiblility property.
    public void Execute(GameObject context, float normalizedTime)
    {
        var controller = TryGetVisibilityController(context);
        if (controller) controller.SetVisibility(VisibilityGroupName, Visiblility);
    }
}