using System.Text.Json.Serialization;

namespace trojan4win.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessFilterMode
{
    ExcludeListed,
    IncludeOnlyListed,
}
