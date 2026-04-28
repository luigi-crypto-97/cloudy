namespace FriendMap.Api.Data;

public class MediaStorageOptions
{
    public string Provider { get; set; } = "local";
    public string LocalRootPath { get; set; } = "wwwroot";
    public string PublicBaseUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Region { get; set; } = "eu-west-1";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public bool ForcePathStyle { get; set; } = true;
    public bool UsePrivateBucket { get; set; } = true;
    public int SignedUrlMinutes { get; set; } = 15;
}
