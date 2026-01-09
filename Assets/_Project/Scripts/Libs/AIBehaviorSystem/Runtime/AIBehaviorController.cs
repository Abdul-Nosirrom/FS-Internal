using Unity.Behavior;
using UnityEngine;

namespace FS.AI
{
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public class AIBehaviorController : MonoBehaviour
    {
        private BehaviorGraphAgent m_behaviorAgent;
        
        private void Awake()
        {
            m_behaviorAgent = GetComponent<BehaviorGraphAgent>();
        }
    }
}