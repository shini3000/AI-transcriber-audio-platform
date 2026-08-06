using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;

namespace Capstone.Application.UseCases;

public class GetAudioResultUseCase
{
    private readonly IAudioFileRepository _audioFileRepository;
    private readonly IAudioTranscriptionRepository _transcriptionRepository;

    public GetAudioResultUseCase(
        IAudioFileRepository audioFileRepository,
        IAudioTranscriptionRepository transcriptionRepository)
    {
        _audioFileRepository = audioFileRepository;
        _transcriptionRepository = transcriptionRepository;
    }

    public async Task<AudioResultResponse?> ExecuteAsync(Guid audioFileId)
    {
        var audioFile = await _audioFileRepository.GetByIdAsync(audioFileId);
        if (audioFile is null)
            return null;

        var transcription = await _transcriptionRepository.GetByAudioFileIdAsync(audioFileId);
        if (transcription is null)
        {
            return new AudioResultResponse(
                "pending",
                audioFile.Id,
                audioFile.FileName,
                null,
                null);
        }

        var summary = transcription.Summary;
        if (summary is null)
        {
            return new AudioResultResponse(
                "pending",
                audioFile.Id,
                audioFile.FileName,
                null,
                null);
        }

        return new AudioResultResponse(
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
                summary.CreatedAt));
    }
}
