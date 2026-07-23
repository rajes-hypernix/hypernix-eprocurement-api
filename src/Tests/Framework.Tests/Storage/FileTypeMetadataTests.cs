using FSH.Framework.Storage;

namespace Framework.Tests.Storage;

public sealed class FileTypeMetadataTests
{
    #region Happy Path

    [Fact]
    public void GetRules_Should_ReturnImageRules_When_ImageRequested()
    {
        // Act
        var rules = FileTypeMetadata.GetRules(FileType.Image);

        // Assert
        rules.MaxSizeInMB.ShouldBe(5);
        rules.AllowedExtensions.ShouldContain(".png");
        rules.AllowedExtensions.ShouldContain(".jpg");
        rules.AllowedExtensions.ShouldContain(".jpeg");
        rules.AllowedExtensions.ShouldContain(".ico");
    }

    [Fact]
    public void GetRules_Should_ReturnPdfRules_When_PdfRequested()
    {
        // Act
        var rules = FileTypeMetadata.GetRules(FileType.Pdf);

        // Assert
        rules.MaxSizeInMB.ShouldBe(10);
        rules.AllowedExtensions.ShouldHaveSingleItem();
        rules.AllowedExtensions.ShouldContain(".pdf");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetRules_Should_ReturnDocumentRules_When_DocumentRequested()
    {
        var rules = FileTypeMetadata.GetRules(FileType.Document);

        rules.MaxSizeInMB.ShouldBe(20);
        rules.AllowedExtensions.ShouldContain(".pdf");
        rules.AllowedExtensions.ShouldContain(".docx");
        rules.AllowedExtensions.ShouldContain(".png");
    }

    [Fact]
    public void GetRules_Should_Throw_When_TypeUnsupported()
    {
        Should.Throw<NotSupportedException>(() => FileTypeMetadata.GetRules((FileType)999));
    }

    [Fact]
    public void FileValidationRules_Should_DefaultToFiveMb_When_NotSet()
    {
        // Arrange & Act
        var rules = new FileValidationRules();

        // Assert
        rules.MaxSizeInMB.ShouldBe(5);
        rules.AllowedExtensions.ShouldBeEmpty();
    }

    #endregion
}
