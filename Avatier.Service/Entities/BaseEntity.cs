using System;
using System.Collections.Generic;
using System.Text;

namespace Avatier.Service.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset RegisteredAt { get; set; }
    }
}
