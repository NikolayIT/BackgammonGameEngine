namespace Backgammon.Logic.Tests.Support
{
    using System.Text.Json.Serialization;

    /// <summary>A source-generated JSON context configured like ednaigra.com's game contexts.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(BackgammonSeatView))]
    [JsonSerializable(typeof(BackgammonAction))]
    [JsonSerializable(typeof(BackgammonMatchRecord))]
    internal sealed partial class HostJsonContext : JsonSerializerContext
    {
    }
}
