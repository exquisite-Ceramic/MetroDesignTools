namespace MetroToolKits.SectionGenerator.App.Models;

[Flags]
public enum ConvertedElementMetadataIssueReason
{
    None = 0,
    MissingBackupRecord = 1,
    MissingTemplateId = 2,
    MissingConvertedType = 4,
    AmbiguousTemplate = 8,
    NoTemplateCandidate = 16
}
