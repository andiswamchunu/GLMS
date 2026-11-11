namespace GLMS.Web.Services;

/// <summary>
/// Validates the "Signed Agreement" upload. Only genuine PDF files are accepted:
/// the extension must be .pdf AND the content must start with the PDF signature, so a renamed .exe is rejected too.
/// </summary>
public static class AgreementFileValidator
{
    public const long MaxBytes = 10L * 1024 * 1024; // 10 MB
    public const string AllowedExtension = ".pdf";

    public static void Validate(string? fileName, long length, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidFileException("Please choose a PDF file to upload.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != AllowedExtension)
        {
            var shown = string.IsNullOrEmpty(extension) ? "files without an extension" : $"'{extension}' files";
            throw new InvalidFileException($"Only PDF files (.pdf) are allowed; {shown} are not accepted.");
        }

        if (length <= 0)
        {
            throw new InvalidFileException("The selected file is empty.");
        }

        if (length > MaxBytes)
        {
            throw new InvalidFileException($"The file is too large. The maximum size is {MaxBytes / (1024 * 1024)} MB.");
        }

        if (!content.CanSeek)
        {
            throw new InvalidOperationException("The upload stream must be seekable so it can be inspected.");
        }

        Span<byte> header = stackalloc byte[5];
        var start = content.Position;
        var read = 0;
        while (read < header.Length)
        {
            var n = content.Read(header[read..]);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        content.Position = start;

        if (read < header.Length || !header.SequenceEqual("%PDF-"u8))
        {
            throw new InvalidFileException("The file is not a valid PDF document.");
        }
    }
}
