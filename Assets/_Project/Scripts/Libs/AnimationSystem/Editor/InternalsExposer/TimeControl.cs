using UnityEditor;
using UnityEngine;

namespace FS.Animation.Editor.Internals
{
    public class TimeControlAPI
    {
        internal TimeControl m_timeControl;

        public TimeControlAPI()
        {
            m_timeControl = new TimeControl();
        }

        public void Update() => m_timeControl.Update();
        public void DoTimeControl(Rect rect) => m_timeControl.DoTimeControl(rect);

        public float startTime
        {
            get => m_timeControl.startTime;
            set => m_timeControl.startTime = value;
        }
        public float stopTime
        {
            get => m_timeControl.stopTime;
            set => m_timeControl.stopTime = value;
        }
        public float currentTime
        {
            get => m_timeControl.currentTime;
            set => m_timeControl.currentTime = value;
        }
        public float normalizedTime
        {
            get => m_timeControl.normalizedTime;
            set => m_timeControl.normalizedTime = value;
        }
        public float playbackSpeed
        {
            get => m_timeControl.playbackSpeed;
            set => m_timeControl.playbackSpeed = value;
        }
        public float deltaTime
        {
            get => m_timeControl.deltaTime;
            set => m_timeControl.deltaTime = value;
        }
        public bool playing
        {
            get => m_timeControl.playing;
            set => m_timeControl.playing = value;
        }
        public bool loop
        {
            get => m_timeControl.loop;
            set => m_timeControl.loop = value;
        }
    }
}