using System;

namespace FS.GameService.Attributes
{
    public enum EGameServiceRegistrationOrder
    {
        Early, Normal, Late
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisteredService : Attribute
    {
        public readonly EGameServiceRegistrationOrder RegistrationOrder = EGameServiceRegistrationOrder.Normal;

        public AutoRegisteredService() {}
        public AutoRegisteredService(EGameServiceRegistrationOrder order)
        {
            RegistrationOrder = order;
        }
    }
}