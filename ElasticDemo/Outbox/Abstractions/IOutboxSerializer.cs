using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.Abstractions
{
    public interface IOutboxSerializer
    {
        string Serialize(object obj);
        T Deserialize<T>(string payload);
    }
}
