using System;
using System.Collections.Generic;
using FS.Animation;
using FS.Player;
using Sirenix.OdinInspector;
using TimeUtils;
using UnityEngine;
using UnityEngine.Assertions;

public class WeaponExtensions : MonoBehaviour
{
    public enum Type
    {
        FingerGuns, SpringLegs, BoxingGloves, WhipArms
    }

    [Title("Style Actions")]
    [ChildGameObjectsOnly][SerializeField] private GameObject m_springLegActions;
    [ChildGameObjectsOnly][SerializeField] private GameObject m_fingerGunActions;
    [ChildGameObjectsOnly][SerializeField] private GameObject m_boxingGloveActions;
    [ChildGameObjectsOnly][SerializeField] private GameObject m_whipArmActions;
    
    public event Action<Type, Type> OnEquippedChanged;

    private Type m_equipped = Type.SpringLegs;
    public Type Equipped
    {
        get => m_equipped;
        set
        {
            var prev = m_equipped;
            if (prev == value) return;
            m_equipped = value;
            m_sinceLastStyleSwitch = 0;
            OnStyleSwitch(prev, m_equipped);
        }
    }
    
    private const float k_styleSwitchCooldown = 0.2f;
    private TimeSince m_sinceLastStyleSwitch;

    private PlayerInputSystem m_input;
    private FSAnimator m_animator;
    private RenderVisibilityController m_visibilityController;

    private void Start()
    {
        m_input = PlayerManager.Instance.GetSubsystem<PlayerInputSystem>(gameObject);
        m_animator = GetComponentInChildren<FSAnimator>();
        m_visibilityController = GetComponentInChildren<RenderVisibilityController>();
        OnStyleSwitch(Equipped, Equipped); // Initialize!
        Assert.IsNotNull(m_input, "PlayerInputSystem is not found on the player object.");
    }
    
    private void OnStyleSwitch(Type previous, Type current)
    {
        // Disable prev mesh and show new one
        m_visibilityController.Show(current.ToString()); // Set as exclusive sets with the others so showing this auto-hides the rest
        
        // Disable all action sets
        m_springLegActions?.SetActive(false);
        m_fingerGunActions?.SetActive(false);
        m_boxingGloveActions?.SetActive(false);
        m_whipArmActions?.SetActive(false);
        
        OnEquippedChanged?.Invoke(previous, current);
        
        // Enable only necessary
        switch (current)
        {
            case Type.SpringLegs:
                m_springLegActions?.SetActive(true);
                break;
            case Type.BoxingGloves:
                m_boxingGloveActions?.SetActive(true);
                break;
            case Type.FingerGuns:
                m_fingerGunActions?.SetActive(true);
                break;
            case Type.WhipArms:
                m_whipArmActions?.SetActive(true);
                break; // Holster vs rope managed in update
        }
    }

    private void LateUpdate()
    {
        if (m_sinceLastStyleSwitch < k_styleSwitchCooldown) return;
        
        var selectedExtension = -1;
        
        if (m_input.GetButton(GameInput.SpringLegs)) selectedExtension = (int)Type.SpringLegs; 
        else if (m_input.GetButton(GameInput.FingerGuns)) selectedExtension = (int)Type.FingerGuns; 
        else if (m_input.GetButton(GameInput.BoxingGloves)) selectedExtension = (int)Type.BoxingGloves; 
        else if (m_input.GetButton(GameInput.WhipArms)) selectedExtension = (int)Type.WhipArms;

        if (selectedExtension >= 0)
        {
            Equipped = (Type) selectedExtension;
        }
    }
    
    // Temp DEBUG UI
    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(25f, Screen.height - 100f, 500, 100), $"{Equipped}", style);
    }
}