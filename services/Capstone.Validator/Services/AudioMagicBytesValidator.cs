namespace Capstone.Validator.Services;

public record ValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static ValidationResult Ok() => new(true);
    public static ValidationResult Fail(string reason) => new(false, reason);
}

public class AudioMagicBytesValidator
{
    private readonly ILogger<AudioMagicBytesValidator> _logger;

    private const int HeaderBufferSize = 12;

    public AudioMagicBytesValidator(ILogger<AudioMagicBytesValidator> logger)
    {
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(string filePath, string declaredContentType)
    {
        if (!File.Exists(filePath))
            return ValidationResult.Fail("File not found for validation.");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return ValidationResult.Fail("File is empty.");

        byte[] header;
        try
        {
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bufferSize = (int)Math.Min(HeaderBufferSize, fileInfo.Length);
            header = new byte[bufferSize];
            _ = await fs.ReadAsync(header);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read header bytes from {FilePath}", filePath);
            return ValidationResult.Fail("Unable to read file for validation.");
        }

        var result = InspectMagicBytes(header, declaredContentType);
        if (!result.IsValid)
        {
            _logger.LogWarning("Magic bytes validation failed for '{FilePath}' (declared: {ContentType}): {Reason}",
                filePath, declaredContentType, result.ErrorMessage);
        }
        else
        {
            _logger.LogDebug("Magic bytes OK for '{FilePath}' (declared: {ContentType})", filePath, declaredContentType);
        }

        return result;
    }

    private static ValidationResult InspectMagicBytes(byte[] h, string contentType)
    {
        if (contentType is "audio/mpeg")
        {
            if (IsMp3(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid MP3 (missing ID3 tag or MPEG sync frame).");
        }

        if (contentType is "audio/wav" or "audio/x-wav")
        {
            if (IsWav(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid WAV (missing RIFF/WAVE header).");
        }

        if (contentType is "audio/ogg")
        {
            if (IsOgg(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid OGG (missing OggS header).");
        }

        if (contentType is "audio/flac")
        {
            if (IsFlac(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid FLAC (missing fLaC header).");
        }

        if (contentType is "audio/mp4")
        {
            if (IsMp4(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid MP4/M4A (missing ftyp box).");
        }

        if (contentType is "audio/aac")
        {
            if (IsAac(h))
                return ValidationResult.Ok();
            return ValidationResult.Fail("File does not appear to be a valid AAC (missing ADTS sync word or ftyp box).");
        }

        if (IsMp3(h) || IsWav(h) || IsOgg(h) || IsFlac(h) || IsMp4(h) || IsAac(h))
        {
            return ValidationResult.Ok();
        }

        return ValidationResult.Fail($"Unsupported or non-audio file format (declared: '{contentType}'). Allowed formats: MP3, WAV, OGG, FLAC, MP4, AAC.");
    }

    private static bool IsMp3(byte[] h)
    {
        if (h.Length >= 3 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
            return true;

        if (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0)
            return true;

        return false;
    }

    private static bool IsWav(byte[] h)
    {
        return h.Length >= 12 &&
               h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' &&
               h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E';
    }

    private static bool IsOgg(byte[] h)
    {
        return h.Length >= 4 && h[0] == 'O' && h[1] == 'g' && h[2] == 'g' && h[3] == 'S';
    }

    private static bool IsFlac(byte[] h)
    {
        return h.Length >= 4 && h[0] == 'f' && h[1] == 'L' && h[2] == 'a' && h[3] == 'C';
    }

    private static bool IsMp4(byte[] h)
    {
        return h.Length >= 8 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p';
    }

    private static bool IsAac(byte[] h)
    {
        if (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xF0) == 0xF0)
            return true;

        if (h.Length >= 8 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p')
            return true;

        return false;
    }
}
