using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;

namespace Capstone.Application.UseCases;

public class GetAudioFilesListUseCase
{
    private readonly IAudioFileRepository _audioFileRepository;

    public GetAudioFilesListUseCase(IAudioFileRepository audioFileRepository)
    {
        _audioFileRepository = audioFileRepository;
    }

    public async Task<List<AudioResultResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var records = await _audioFileRepository.GetAllWithTranscriptionAsync(cancellationToken);
        
        var responses = new List<AudioResultResponse>();

        foreach (var (audioFile, transcription) in records)
        {
            if (transcription is null)
            {
                responses.Add(new AudioResultResponse(
                    "pending",
                    audioFile.Id,
                    audioFile.FileName,
                    null,
                    null));
                continue;
            }

            var summary = transcription.Summary;
            if (summary is null)
            {
                responses.Add(new AudioResultResponse(
                    "pending",
                    audioFile.Id,
                    audioFile.FileName,
                    null,
                    null));
                continue;
            }

            responses.Add(new AudioResultResponse(
                "ready",
                audioFile.Id,
                audioFile.FileName,
                new TranscriptionDto(
                    transcription.FullText,
                    transcription.ModelName,
                    transcription.ProcessingTimeMs,
                    transcription.CreatedAt),
                new SummaryDto(
                    summary.SummaryText,
                    summary.ModelName,
                    summary.CreatedAt)));
        }

        return responses;
    }
}
