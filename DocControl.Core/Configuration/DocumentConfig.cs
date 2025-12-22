namespace DocControl.Core.Configuration;

public sealed class DocumentConfig
{
    public int LevelCount { get; set; } = 3; // 3 or 4
    public string Level1Name { get; set; } = "Level1";
    public string Level2Name { get; set; } = "Level2";
    public string Level3Name { get; set; } = "Level3";
    public string Level4Name { get; set; } = "Level4";
    public bool EnableLevel4 { get; set; } = false;
    public int PaddingLength { get; set; } = 3;
    public string Separator { get; set; } = "-";
}
