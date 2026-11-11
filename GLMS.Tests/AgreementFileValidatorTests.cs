using GLMS.Tests.Helpers;
using GLMS.Web.Services;

namespace GLMS.Tests;

public class AgreementFileValidatorTests
{
    [Fact]
    public void Validate_ExeFile_ThrowsInvalidFileException()
    {
        using var stream = new MemoryStream(PdfSamples.ExecutableBytes());

        var ex = Assert.Throws<InvalidFileException>(() =>
            AgreementFileValidator.Validate("setup.exe", stream.Length, stream));

        Assert.Contains(".exe", ex.Message);
        Assert.Contains("PDF", ex.Message);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.bat")]
    [InlineData("macro.js")]
    [InlineData("agreement.docx")]
    [InlineData("scan.png")]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("agreement.pdf.exe")]   // double extension: the real extension is what counts
    [InlineData("agreement")]            // no extension at all
    public void Validate_RestrictedFileType_ThrowsInvalidFileException(string fileName)
    {
        using var stream = PdfSamples.ValidPdfStream(); // even with genuine PDF content, the extension must be .pdf

        Assert.Throws<InvalidFileException>(() => AgreementFileValidator.Validate(fileName, stream.Length, stream));
    }

    [Theory]
    [InlineData("agreement.pdf")]
    [InlineData("Signed Agreement.PDF")]
    [InlineData("contract.v2.final.Pdf")]
    git status
    public void Validate_PdfFile_DoesNotThrow(string fileName)
    {
        using var stream = PdfSamples.ValidPdfStream();

        var ex = Record.Exception(() => AgreementFileValidator.Validate(fileName, stream.Length, stream));

        Assert.Null(ex);
    }

    [Fact]
    public void Validate_PdfFile_LeavesStreamAtStartSoItCanBeSaved()
    {
        using var stream = PdfSamples.ValidPdfStream();

        AgreementFileValidator.Validate("agreement.pdf", stream.Length, stream);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Validate_ExecutableRenamedToPdf_IsRejectedByContentCheck()
    {
        using var stream = new MemoryStream(PdfSamples.ExecutableBytes());

        var ex = Assert.Throws<InvalidFileException>(() =>
            AgreementFileValidator.Validate("totally-a-contract.pdf", stream.Length, stream));

        Assert.Contains("not a valid PDF", ex.Message);
    }

    [Fact]
    public void Validate_FileShorterThanPdfSignature_Throws()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50 }); // "%P"

        Assert.Throws<InvalidFileException>(() => AgreementFileValidator.Validate("a.pdf", stream.Length, stream));
    }

    [Fact]
    public void Validate_EmptyFile_Throws()
    {
        using var stream = new MemoryStream();

        var ex = Assert.Throws<InvalidFileException>(() => AgreementFileValidator.Validate("a.pdf", 0, stream));

        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Validate_FileOverSizeLimit_Throws()
    {
        using var stream = PdfSamples.ValidPdfStream();

        var ex = Assert.Throws<InvalidFileException>(() =>
            AgreementFileValidator.Validate("big.pdf", AgreementFileValidator.MaxBytes + 1, stream));

        Assert.Contains("too large", ex.Message);
    }

    [Fact]
    public void Validate_FileExactlyAtSizeLimit_IsAccepted()
    {
        using var stream = PdfSamples.ValidPdfStream();

        var ex = Record.Exception(() => AgreementFileValidator.Validate("max.pdf", AgreementFileValidator.MaxBytes, stream));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingFileName_Throws(string? fileName)
    {
        using var stream = PdfSamples.ValidPdfStream();

        Assert.Throws<InvalidFileException>(() => AgreementFileValidator.Validate(fileName, stream.Length, stream));
    }
}
