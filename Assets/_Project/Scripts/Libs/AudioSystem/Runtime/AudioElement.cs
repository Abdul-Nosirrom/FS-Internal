using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FS.Audio
{
    [Serializable]
    public struct AudioParameter
    {
        [SerializeField] public string Name;
        [SerializeField] public float Value;
        
        public AudioParameter(string name, float value)
        {
            Name = name;
            Value = value;
        }
    }
    
    [Serializable]
    public struct AudioElement
    {
        [field: SerializeField]
        public EventReference AudioEvent { get; private set; }
        
        public List<AudioParameter> Parameters;

        public bool IsNull => AudioEvent.IsNull;

        public override string ToString() => AudioEvent.ToString();

        // private PARAMETER_ID m_parameterId;
        // public PARAMETER_ID ParameterId
        // {
        //     get
        //     {
        //         if (m_parameterId == default)
        //         {
        //             m_parameterId = AudioManager.GetParameterID(AudioEvent);
        //         }
        //         return m_parameterId;
        //     }
        // }
        //

        // private Dictionary<string, float> m_parameterMap;
        // public Dictionary<string, float> ParameterMap
        // {
        //     get
        //     {
        //         EventInstance t;
        //         t.param
        //         m_parameterMap ??= new();
        //         foreach (var param in Parameters)
        //         {
        //             m_parameterMap[param.Name] = param.Value;
        //         }
        //         return m_parameterMap;
        //     }
        // }
    }
}