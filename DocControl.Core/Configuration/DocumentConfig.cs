namespace DocControl.Core.Configuration;

public sealed class DocumentConfig
{
    public int LevelCount { get; set; } = 3; // 1-6
    public string Level1Name { get; set; } = "Level1";
    public string Level2Name { get; set; } = "Level2";
    public string Level3Name { get; set; } = "Level3";
    public string Level4Name { get; set; } = "Level4";
    public string Level5Name { get; set; } = "Level5";
    public string Level6Name { get; set; } = "Level6";
    public int Level1Length { get; set; } = 3;
    public int Level2Length { get; set; } = 3;
    public int Level3Length { get; set; } = 3;
    public int Level4Length { get; set; } = 3;
    public int Level5Length { get; set; } = 3;
    public int Level6Length { get; set; } = 3;
    public int PaddingLength { get; set; } = 3;
    public string Separator { get; set; } = "-";
}
