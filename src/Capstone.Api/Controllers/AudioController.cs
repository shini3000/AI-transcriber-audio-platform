using Capstone.Application.DTOs;
using Capstone.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private static readonly string[] AllowedAudioTypes =
    [
        "audio/mpeg",
        "audio/wav",
        "audio/ogg",
        "audio/mp4",
        "audio/aac",
        "audio/flac",
        "audio/x-wav",
    ];

    private readonly UploadAudioUseCase _useCase;

    public AudioController(UploadAudioUseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(UploadAudioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was provided or the file is empty." });

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedAudioTypes.Contains(file.ContentType))
        {
            return BadRequest(new
            {
                error = $"Unsupported audio format: '{file.ContentType}'. " +
                        $"Allowed: mp3, wav, ogg, mp4, aac, flac."
            });
        }

        await using var stream = file.OpenReadStream();

        var request = new UploadAudioRequest
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length,
            FileStream = stream
        };

        var response = await _useCase.ExecuteAsync(request, cancellationToken);

        return Created(string.Empty, response);
    }
}
