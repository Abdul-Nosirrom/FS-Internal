using System;
using System.Collections.Generic;
using System.Linq;
using Drawing;
using TimeUtils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FS.AI
{
    public struct CombatCircleSettings
    {
        public int RingCount;    
        public int SectorsPerRing;
        public float[] RingRadii;
        
        public static CombatCircleSettings Default => new CombatCircleSettings()
        {
            RingCount = 3,
            SectorsPerRing = 6,
            RingRadii = new float[] {3f, 6f, 8f}
        };

        public static CombatCircleSettings Create(int ringCount, int sectorsPerRing, float[] ringRadii) =>
            new CombatCircleSettings()
            {
                RingCount = ringCount,
                SectorsPerRing = sectorsPerRing,
                RingRadii = ringRadii
            };
    }
    
    public class CombatSlot : IEquatable<CombatSlot>
    {
        public readonly int Ring;
        public readonly int Sector;
        public readonly int Priority;
        public bool IsBlocked;

        public bool Equals(CombatSlot other) => Ring == other.Ring && Sector == other.Sector;
        public override int GetHashCode() => HashCode.Combine(Ring, Sector);

        public float m_angle;
        public float m_radius;
        private Vector3 m_localPosition;
        
        public readonly CombatSlot[] Neighbors = new CombatSlot[4]; // Immediate neighbors (+- 1 ring / +- 1 sector)

        public CombatSlot(int ring, int sector, int priority, CombatCircleSettings settings)
        {
            Ring = ring;
            Sector = sector;
            Priority = priority;
            IsBlocked = false;
            Init(settings);
        }
        
        public void Init(CombatCircleSettings settings)
        {
            m_angle = (Sector / (float)settings.SectorsPerRing) * 360f;
            m_radius = settings.RingRadii[Ring];
            
            float rad = m_angle * Mathf.Deg2Rad;
            m_localPosition = new Vector3(Mathf.Cos(rad) * m_radius, 0, Mathf.Sin(rad) * m_radius);
        }

        public Vector3 GetWorldPosition(Vector3 center) => m_localPosition + center;
    }

    public class SlotAssignment
    {
        public CombatSlot Slot;
        public GameObject Occupant;
        public TimeSince TimeSinceAssigned;
        public bool IsBlocked => Slot.IsBlocked;
        public bool IsOccupied => Occupant != null;
    }
    
    public class CombatCircle
    {
        // Circle w/ sectors that can be claimed & unclaimed by agents. Circle is centered around a given combat target
        
        public Transform Center { get; private set; }

        private CombatCircleSettings m_settings = CombatCircleSettings.Default;

        private Dictionary<CombatSlot, SlotAssignment> m_slots = new();
        private List<CombatSlot> m_orderedSlots = new();
        
        public CombatCircle(Transform center)
        {
            Center = center;
            InitializeSectors();
        }

        private void InitializeSectors()
        {
            for (int ring = 0; ring < m_settings.RingCount; ring++)
            {
                for (int sector = 0; sector < m_settings.SectorsPerRing; sector++)
                {
                    var slot = new CombatSlot(ring, sector, CalculateSlotPriority(ring, sector), m_settings);
                    m_slots[slot] = new SlotAssignment() { Slot = slot };
                    m_orderedSlots.Add(slot);
                }
            }
            
            // Initialize neighbors
            foreach (var slot in m_slots.Keys)
            {
                slot.Neighbors[0] = m_slots.Keys.FirstOrDefault(s => s.Ring == slot.Ring && s.Sector == (slot.Sector + 1) % m_settings.SectorsPerRing); // + sector
                slot.Neighbors[1] = m_slots.Keys.FirstOrDefault(s => s.Ring == slot.Ring && s.Sector == (slot.Sector - 1 + m_settings.SectorsPerRing) % m_settings.SectorsPerRing); // - sector
                slot.Neighbors[2] = m_slots.Keys.FirstOrDefault(s => s.Ring == slot.Ring + 1 && s.Sector == slot.Sector); // + ring
                slot.Neighbors[3] = m_slots.Keys.FirstOrDefault(s => s.Ring == slot.Ring - 1 && s.Sector == slot.Sector); // - ring
            }
            
            m_orderedSlots.Sort((a , b) => b.Priority.CompareTo(a.Priority));
        }

        private int CalculateSlotPriority(int ring, int sector)
        {
            int ringPriority = 0;
            int sectorPriority = 0;
            return ring;//ringPriority + sectorPriority;
        }

        public bool RequestPosition(GameObject agent, AIDirector.CombatRole role, out Vector3 position)
        {
            ValidateSlots();
            
            position = agent.transform.position;
            
            if (!FindBestSlotForRole(role, agent, out var bestSlot)) return false;

            // Release any previous slot occupied by this agent, we're giving them a new slot (or the same)
            ReleaseAgent(agent);
            
            // Assign new slot
            m_slots[bestSlot] = new SlotAssignment()
            {
                Slot = bestSlot,
                Occupant = agent,
                TimeSinceAssigned = 0
            };

            position = GetSlotWorldPosition(bestSlot);
            return true;
        }
        
        private bool FindBestSlotForRole(AIDirector.CombatRole role, GameObject agent, out CombatSlot bestSlot)
        {
            bestSlot = null;

            // First try role-preferred slots
            float bestScore = -float.MaxValue;
            foreach (var slot in m_orderedSlots)
            {
                if (!slot.IsBlocked && m_slots[slot].Occupant == agent)
                {
                    // reset if too far or out of that sector or something
                    bestSlot = slot;
                    return true;
                }
                
                if (slot.IsBlocked || m_slots[slot].IsOccupied) continue;

                var slotScore = CalculateCombatSlotScore(slot, agent.transform.position, role);

                if (slotScore > bestScore)
                {
                    bestSlot = slot;
                    bestScore = slotScore;
                }
            }

            return bestSlot != null;
        }

        private float CalculateCombatSlotScore(CombatSlot slot, Vector3 position, AIDirector.CombatRole role)
        {
            float ringScore = -float.MaxValue;
            float sectorScore = -float.MaxValue;
            
            // Calculate ring score, for ranged prefer higher, hybrid mid, melee low
            {
                float normalizedRing = slot.Ring / (float)(m_settings.RingCount - 1);
                ringScore = role switch
                {
                    AIDirector.CombatRole.Melee => 1f - normalizedRing, // Prefer lower rings
                    AIDirector.CombatRole.Hybrid => 1f - Mathf.Abs(0.5f - normalizedRing) * 2f, // Prefer mid rings
                    AIDirector.CombatRole.Ranged => normalizedRing, // Prefer higher rings
                    _ => 0f
                };
                // Add some randomness to avoid perfect clustering
                float ringDelta = 1 / (float)(m_settings.RingCount - 1);
                ringScore += 2f * Random.Range(-ringDelta, ringDelta);
                ringScore = Mathf.Clamp01(ringScore);
                //return ringScore;
            }
            
            // Calculate sector score, prefer sectors closest to agent position while at the same time have empty neighbors
            // Each sector has 4 immediate neighbors, so each empty neighbor adds a higher score
            {
                float agentToTargetDist = Vector3.Distance(position, Center.position);
                float agentToSlotDist = Vector3.Distance(position, GetSlotWorldPosition(slot));

                // Penalize sectors further away than the target
                float penalizationCrossingTarget = 0f;
                if (agentToSlotDist > agentToTargetDist) penalizationCrossingTarget -= 1f;

                int numNeighbors = 0; // Some can be null like ring 0 has 3 neighbors not 4
                int emptyNeighbors = 0;
                foreach (var neighbor in slot.Neighbors)
                {
                    if (neighbor == null) continue;
                    numNeighbors++;
                    if (!m_slots[neighbor].IsOccupied || !neighbor.IsBlocked)
                        emptyNeighbors++;
                }
                
                float neighborScore = numNeighbors > 0 ? (emptyNeighbors / (float)numNeighbors) : 0f;
                
                // Can maybe add a facing score, e.g prefer sectors the player is facing TODO
                
                sectorScore = 0.7f * neighborScore + penalizationCrossingTarget;
            }

            return 0.5f * ringScore + 0.5f * sectorScore;
        }

        public void ReleaseAgent(GameObject agent)
        {
            foreach (var kvp in m_slots)
            {
                if (kvp.Value.Occupant == agent)
                {
                    m_slots[kvp.Key] = new SlotAssignment() { Slot = kvp.Key };
                    return;
                }
            }
        }

        /// <summary>
        /// Validate sectors/slots & mark them as blocked if they are not valid (out of navmesh, too close to obstacles, etc)
        /// </summary>
        public void ValidateSlots()
        {
            foreach (var slot in m_slots.Keys)
            {
                slot.IsBlocked = !IsSectorValid(slot);
            }
        }
        
        public Vector3 GetSlotWorldPosition(CombatSlot slot)
        {
            var pos = slot.GetWorldPosition(Center.position);
            pos += GetSectorRandomization(slot) * 0.5f; // Add some randomization to avoid perfect grid look
            return pos; // We don't do the rotation relative to the centers facing
        }

        private Vector3 GetSectorRandomization(CombatSlot slot)
        {
            //Random.InitState(slot.GetHashCode());
            var randomOffset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            //Random.InitState((int)Time.time); // Reset
            return randomOffset;
        }

        private static Collider[] s_overlapResults = new Collider[4];

        private bool IsSectorValid(CombatSlot slot)
        {
            // Test sector center & 4 corners
            var centerPos = GetSlotWorldPosition(slot);
            if (!IsPositionValid(centerPos)) return false;
            
            foreach (var neighbor in slot.Neighbors)
            {
                if (neighbor == null) continue;
                var neighborPos = GetSlotWorldPosition(neighbor);
                var neighborMidPos = (centerPos + neighborPos) * 0.5f;
                if (!IsPositionValid(neighborMidPos)) return false;
            }

            return true;
        }
        
        /// <summary>
        /// Checks the validity of a position, e.g is it on the navmesh & not close to obstacles
        /// </summary>
        private bool IsPositionValid(Vector3 position)
        {
            if (!AstarPath.active.IsPointOnNavmesh(position)) return false;
            if (Physics.OverlapSphereNonAlloc(position, 0.3f, s_overlapResults,
                    PhysicsLayers.GetLayerCollisionMask(PhysicsLayers.CameraObstruction)) > 0) return false;
            
            return true;
        }

        #region Debug Draw

        public void DrawDebug()
        {
            var posColor = Color.darkOrange;
            var blockedColor = Color.crimson;
            var occupiedColor = Color.midnightBlue;
            var freeColor = Color.forestGreen;
            freeColor.a = 0.4f;
            
            var halfAngle = 360f / (m_settings.SectorsPerRing * 2);

            var drawPos = Center.position - 0.7f * Vector3.up;

            foreach (var slot in m_orderedSlots)
            {
                var slotCenter = slot.GetWorldPosition(drawPos);
                Draw.ingame.WireSphere(slotCenter, 0.3f, posColor);

                var arcStartAngle = (slot.m_angle - halfAngle) * Mathf.Deg2Rad;
                var arcEndAngle = (slot.m_angle + halfAngle) * Mathf.Deg2Rad;
                
                var arcStartPos = drawPos + new Vector3(Mathf.Cos(arcStartAngle), 0, Mathf.Sin(arcStartAngle)) * slot.m_radius;
                var arcEndPos = drawPos + new Vector3(Mathf.Cos(arcEndAngle), 0, Mathf.Sin(arcEndAngle)) * slot.m_radius;

                // Outlines
                {
                    using var thickness = Draw.ingame.WithLineWidth(3f);
                    Draw.ingame.Arc(drawPos, arcStartPos, arcEndPos, Color.black);
                    Draw.ingame.Line(drawPos, arcStartPos, Color.black);
                    Draw.ingame.Line(drawPos, arcEndPos, Color.black);
                }

                if (slot.IsBlocked)
                {
                    Debug.LogError($"Slot Is Blocked In Gizmo");
                }

                var arcColor = slot.IsBlocked
                    ? blockedColor
                    : (m_slots[slot].IsOccupied ? occupiedColor : freeColor);
                
                Draw.ingame.SolidArc(drawPos, arcStartPos, arcEndPos, arcColor);
            }
        }
        
        #endregion
    }
}