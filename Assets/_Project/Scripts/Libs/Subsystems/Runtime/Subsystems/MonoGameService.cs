using System;
using UnityEngine;

namespace FS.GameService
{
    public abstract class MonoGameService : MonoBehaviour, IGameService
    {
        protected void Reset()
        {
            name = GetType().Name;
        }

        public abstract void Initialize();
        public abstract void Shutdown();
    }
}