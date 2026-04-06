using Avatier.Service.Enums;

namespace Avatier.Service.Wrappers
{
    public class Message
    {
        public required string Text { get; init; }
        public required MessageTypeEnum Type { get; init; }
        public Dictionary<string, object> Data { get; init; } = [];
    }
}
