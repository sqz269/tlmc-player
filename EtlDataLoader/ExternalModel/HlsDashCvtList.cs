using Newtonsoft.Json;

namespace EtlDataLoader.ExternalModel;

public class HlsDashCvtList
{
    [JsonProperty("output_mpd")]
    public string OutputMpd { get; set; }
}