using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FS.GameService.Attributes;
using FS.Extensions;
using FS.Patterns;
using UnityEditor;
using UnityEngine;

namespace FS.GameService
{
    public class GameServiceLocator : PersistentSingleton<GameServiceLocator>
    {
        private readonly Dictionary<Type, IGameService> m_Services = new();
        
        #region INITIALIZATION & SHUTDOWN

        /// <summary>
        /// Ensure an instance is initialized on game-boot. Simply done by referencing the singleton instance.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void BootStrap() => Instance.Initialize();

        /// <summary>
        /// During initialization, fetch all auto-registered services and initialize them in order of their registration order
        /// </summary>
        private void Initialize()
        {
            foreach (var serviceType in ReflectionUtility.GetAllTypesWithAttribute<AutoRegisteredService>()
                         .Where(type => typeof(IGameService).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                         .OrderBy(x => x.GetCustomAttribute<AutoRegisteredService>().RegistrationOrder))
            {
                if (IsRegistered(serviceType)) continue;
                
                CreateAndRegister(serviceType);
            }
        }
        
        /// <summary>
        /// When the service locator is destroyed (during game shutdown), we need to ensure all services are properly shut down.
        /// NOTE: It's not guaranteed that this will be the last object destroyed, so be careful with dependencies unregistering during
        /// game shutdown!
        /// </summary>
        private void OnDestroy()
        {
            // Shutdown all services
            var services = m_Services.Keys.ToArray();
            for (int s = services.Length - 1; s >= 0; s--)
            {
                UnRegister(services[s]);
            }

            m_Services.Clear();
        }
        
        #endregion
        
        #region PUBLIC API

        public static void Register<TService> (TService service) where TService : IGameService => Register(service, typeof(TService));

        public static void UnRegister<TService>() where TService : IGameService => UnRegister(typeof(TService));
        
        public static TService Get<TService>(bool bForced = false) where TService : IGameService, new()
        {
            if (!HasInstance) return default;
            
            var serviceType = typeof(TService);
            if (IsRegistered<TService>())
            {
                return (TService) Instance.m_Services[serviceType];
            }

            if (!bForced) return default;
                //throw new ServiceLocatorException($"Service of type {serviceType.Name} is not registered.");
            
            // If forced, create it if not found
            return (TService) CreateAndRegister(serviceType);
        }

        public static bool IsRegistered(Type t) => HasInstance && Instance.m_Services.ContainsKey(t);
        public static bool IsRegistered<TService>() where TService : IGameService => IsRegistered(typeof(TService));
        
        #endregion
        
        #region PRIVATE API
        
        /// <summary>
        /// Internal non-generic register function for use by CreateAndRegister that's done on initialization
        /// </summary>
        private static void Register(IGameService service, Type type)
        {
            if (!HasInstance) return;
            
            if (IsRegistered(type))
            {
                throw new ArgumentException($"Service of type {type.Name} is already registered.");
            }
            
            service.Initialize();
            Instance.m_Services.Add(type, service);
        }
        
        /// <summary>
        /// If an instance of the service is registered, fetch it and shut it down.
        /// </summary>
        private static void UnRegister(Type type)
        {
            if (!HasInstance) return;
            
            if (!IsRegistered(type))
            {
                throw new ArgumentException($"Service Unregistration Attempt of type {type.Name} is was not found.");
            }

            Instance.m_Services[type].Shutdown();
            Instance.m_Services.Remove(type);
        }
        
        /// <summary>
        /// Helper: Given a type, creates and registers that service. Returns it after creation.
        /// </summary>
        private static IGameService CreateAndRegister(Type serviceType)
        {
            var serviceInst = serviceType.IsMonoBehavior()
                ? (IGameService) FindOrCreateMonoService(serviceType)
                : (IGameService) Activator.CreateInstance(serviceType);

            Register(serviceInst, serviceType);

            return serviceInst;
        }
        
        /// <summary>
        /// Handles creation of monobehavior IGameService, involves creating a new GameObject and attaching the service to it.
        /// </summary>
        private static IGameService FindOrCreateMonoService(Type serviceType)
        {
            // Does it already exist?
            var inGameService = UnityEngine.Object.FindAnyObjectByType(serviceType);

            if (inGameService == null)
            {
                var newObject = new GameObject();
                newObject.AddComponent(serviceType);
                newObject.name = serviceType.Name;
                inGameService = newObject.GetComponent(serviceType);
            }

            return inGameService as IGameService;
        }
        
        #endregion

    }
}