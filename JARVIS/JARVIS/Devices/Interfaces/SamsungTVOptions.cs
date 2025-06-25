using System.ComponentModel.DataAnnotations;

public class SamsungTvOptions
{

    [Required, RegularExpression(@"^[\w\.\-]+$", ErrorMessage = "IpAddress must be a valid host or IP")]
    public string IpAddress { get; set; } = "";

    [Required]
    public string RemoteName { get; set; } = "JARVIS";

    [Range(1, 65535)]
    public int Port { get; set; } = 8001;

    public bool UseSsl { get; set; }
}
