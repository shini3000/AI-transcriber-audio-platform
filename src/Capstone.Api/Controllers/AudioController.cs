using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
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
    private readonly IAudioFileRepository _repository;
    private readonly IFileStorageService _storageService;

    public AudioController(
        UploadAudioUseCase useCase,
        GetAudioResultUseCase getResultUseCase,
        GetAudioFilesListUseCase getListUseCase,
        IAudioFileRepository repository,
        IFileStorageService storageService)
    {
        _useCase = useCase;
        _getResultUseCase = getResultUseCase;
        _getListUseCase = getListUseCase;
        _repository = repository;
        _storageService = storageService;
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

    [HttpGet("{id}/stream/{variant}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Stream(
        string id,
        string variant,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var audioFileId))
            return BadRequest(new { error = "Invalid audio file id." });

        var audioFile = await _repository.GetByIdAsync(audioFileId);
        if (audioFile is null)
            return NotFound(new { error = "Audio file not found." });

        string? storageUrl;
        string contentType;

        if (string.Equals(variant, "original", StringComparison.OrdinalIgnoreCase))
        {
            storageUrl = !string.IsNullOrEmpty(audioFile.OriginalStorageUrl)
                ? audioFile.OriginalStorageUrl
                : audioFile.StorageUrl;
            contentType = !string.IsNullOrEmpty(audioFile.OriginalContentType)
                ? audioFile.OriginalContentType
                : "audio/mpeg";
        }
        else if (string.Equals(variant, "filtered", StringComparison.OrdinalIgnoreCase))
        {
            storageUrl = !string.IsNullOrEmpty(audioFile.FilteredStorageUrl)
                ? audioFile.FilteredStorageUrl
                : audioFile.StorageUrl;
            contentType = !string.IsNullOrEmpty(audioFile.ContentType)
                ? audioFile.ContentType
                : "audio/aac";
        }
        else
        {
            return BadRequest(new { error = "Variant must be 'original' or 'filtered'." });
        }

        if (string.IsNullOrEmpty(storageUrl))
            return NotFound(new { error = "Storage URL not available for requested variant." });

        try
        {
            var stream = await _storageService.GetFileStreamAsync(storageUrl, cancellationToken);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error streaming audio: {ex.Message}" });
        }
    }
}
