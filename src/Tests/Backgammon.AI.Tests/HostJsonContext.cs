namespace Backgammon.AI.Tests
{
    using System.Text.Json.Serialization;

    using Backgammon.Logic;

    /// <summary>A source-generated JSON context configured like ednaigra.com's game contexts.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(BackgammonSeatView))]
    internal sealed partial class HostJsonContext : JsonSerializerContext
    {
    }
}
