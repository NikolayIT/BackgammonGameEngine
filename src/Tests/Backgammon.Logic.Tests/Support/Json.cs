namespace Backgammon.Logic.Tests.Support
{
    using System.Text.Json;

    /// <summary>
    /// JSON the way hosts use it. ednaigra.com serializes with a source-generated context, camelCase names and
    /// RespectRequiredConstructorParameters, and gives bots the view read back from that JSON.
    /// </summary>
    internal static class Json
    {
        public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true };

        public static string Serialize(BackgammonSeatView view) => JsonSerializer.Serialize(view, HostJsonContext.Default.BackgammonSeatView);

        public static BackgammonSeatView Deserialize(string json) => JsonSerializer.Deserialize(json, HostJsonContext.Default.BackgammonSeatView)!;
    }
}
