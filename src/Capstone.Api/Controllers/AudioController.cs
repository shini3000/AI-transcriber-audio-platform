using Capstone.Application.DTOs;
using Capstone.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly UploadAudioUseCase _useCase;
    private readonly GetAudioResultUseCase _getResultUseCase;
    private readonly GetAudioFilesListUseCase _getListUseCase;

    public AudioController(
        UploadAudioUseCase useCase,
        GetAudioResultUseCase getResultUseCase,
        GetAudioFilesListUseCase getListUseCase)
    {
        _useCase = useCase;
        _getResultUseCase = getResultUseCase;
        _getListUseCase = getListUseCase;
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

        await using var stream = file.OpenReadStream();

        var request = new UploadAudioRequest
        {
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Size = file.Length,
            FileStream = stream
        };

        var response = await _useCase.ExecuteAsync(request, cancellationToken);

        return Created(string.Empty, response);
    }

    [HttpGet("{id}/result")]
    [ProducesResponseType(typeof(AudioResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(string id)
    {
        if (!Guid.TryParse(id, out var audioFileId))
            return BadRequest(new { error = "Invalid audio file id." });

        var response = await _getResultUseCase.ExecuteAsync(audioFileId);
        if (response is null)
            return NotFound();

        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AudioResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        var response = await _getListUseCase.ExecuteAsync(cancellationToken);
        return Ok(response);
    }
}
