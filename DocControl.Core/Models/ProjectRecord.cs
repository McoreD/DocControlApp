namespace DocControl.Core.Models;

public sealed class ProjectRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Separator { get; set; } = "-";
    public int PaddingLength { get; set; } = 3;
    public int LevelCount { get; set; } = 3;
    public string Level1Label { get; set; } = "Level1";
    public string Level2Label { get; set; } = "Level2";
    public string Level3Label { get; set; } = "Level3";
    public string Level4Label { get; set; } = "Level4";
    public string Level5Label { get; set; } = "Level5";
    public string Level6Label { get; set; } = "Level6";
    public int Level1Length { get; set; } = 3;
    public int Level2Length { get; set; } = 3;
    public int Level3Length { get; set; } = 3;
    public int Level4Length { get; set; } = 3;
    public int Level5Length { get; set; } = 3;
    public int Level6Length { get; set; } = 3;
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDefault { get; set; }
}
