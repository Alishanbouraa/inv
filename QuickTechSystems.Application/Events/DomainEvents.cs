using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Events
{
    public class EntityChangedEvent<T>
    {
        public string Action { get; set; }
        public T Entity { get; set; }

        public EntityChangedEvent(string action, T entity)
        {
            Action = action;
            Entity = entity;
        }
    }
}
