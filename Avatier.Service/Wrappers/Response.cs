using System;
using System.Collections.Generic;
using System.Text;

namespace Avatier.Service.Wrappers
{
    public record Response
    {
        public List<Message> Messages { get; set; } = [];
        public bool IsInFailure => Messages.Exists(x => x.Type > Enums.MessageTypeEnum.Success);

        public void Merge(Response response)
        {
            Messages.AddRange(response.Messages);
        }
    }

    public record Response<T> : Response
    {
        public T? Data { get; set; }

        public void Merge(Response<T> response)
        {
            base.Merge(response);
            Data = response.Data;
        }

    }
}
