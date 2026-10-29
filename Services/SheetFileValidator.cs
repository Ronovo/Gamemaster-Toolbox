namespace GM_Toolbox.Services
{
    public static class SheetFileValidator
    {
        // Under Kestrel's ~28.6MB default request body limit, so an oversized upload gets this
        // message instead of an unhandled BadHttpRequestException.
        public const long MaxFileSizeBytes = 25 * 1024 * 1024;

        private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

        public static string? Validate(long fileLength, ReadOnlySpan<byte> headerBytes)
        {
            if (fileLength == 0)
            {
                return "Choose a file to upload.";
            }

            if (fileLength > MaxFileSizeBytes)
            {
                return $"That file is larger than the {MaxFileSizeBytes / (1024 * 1024)}MB limit.";
            }

            if (headerBytes.Length < PdfMagicBytes.Length || !headerBytes[..PdfMagicBytes.Length].SequenceEqual(PdfMagicBytes))
            {
                return "That file doesn't look like a PDF.";
            }

            return null;
        }
    }
}
