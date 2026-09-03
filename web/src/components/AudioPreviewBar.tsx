import { useState, useRef, useEffect, useCallback } from 'react'
import {
  Play,
  Pause,
  Volume2,
  VolumeX,
  Sparkles,
  FileAudio,
  RotateCcw,
} from 'lucide-react'
import { resolveAudioUrl } from '../services/api'
import './AudioPreviewBar.css'

export type AudioVariant = 'original' | 'filtered'

interface AudioPreviewBarProps {
  originalUrl?: string | null
  filteredUrl?: string | null
  originalFileName?: string | null
  filteredFileName?: string | null
}

function formatDuration(seconds: number): string {
  if (isNaN(seconds) || seconds < 0) return '0:00'
  const mins = Math.floor(seconds / 60)
  const secs = Math.floor(seconds % 60)
  return `${mins}:${secs.toString().padStart(2, '0')}`
}

export function AudioPreviewBar({
  originalUrl,
  filteredUrl,
  originalFileName,
  filteredFileName,
}: AudioPreviewBarProps) {
  const resolvedOriginal = resolveAudioUrl(originalUrl)
  const resolvedFiltered = resolveAudioUrl(filteredUrl)

  const [activeVariant, setActiveVariant] = useState<AudioVariant>(
    resolvedFiltered ? 'filtered' : 'original',
  )
  const [isPlaying, setIsPlaying] = useState(false)
  const [currentTime, setCurrentTime] = useState(0)
  const [duration, setDuration] = useState(0)
  const [volume, setVolume] = useState(1)
  const [isMuted, setIsMuted] = useState(false)
  const [playbackRate, setPlaybackRate] = useState(1)
  const [hasError, setHasError] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  const audioRef = useRef<HTMLAudioElement | null>(null)

  const currentSourceUrl =
    activeVariant === 'filtered' ? resolvedFiltered : resolvedOriginal

  useEffect(() => {
    if (audioRef.current) {
      audioRef.current.playbackRate = playbackRate
      audioRef.current.volume = isMuted ? 0 : volume
    }
  }, [playbackRate, volume, isMuted])

  const handleVariantSwitch = (variant: AudioVariant) => {
    if (variant === activeVariant) return
    const audio = audioRef.current
    const preservedTime = audio ? audio.currentTime : currentTime
    const wasPlaying = isPlaying

    setActiveVariant(variant)
    setHasError(false)

    setTimeout(() => {
      if (audioRef.current) {
        audioRef.current.currentTime = preservedTime
        if (wasPlaying) {
          audioRef.current.play().catch(() => setIsPlaying(false))
        }
      }
    }, 50)
  }

  const togglePlay = useCallback(() => {
    const audio = audioRef.current
    if (!audio) return

    if (isPlaying) {
      audio.pause()
      setIsPlaying(false)
    } else {
      setHasError(false)
      setIsLoading(true)
      audio
        .play()
        .then(() => {
          setIsPlaying(true)
          setIsLoading(false)
        })
        .catch(() => {
          setIsPlaying(false)
          setIsLoading(false)
          setHasError(true)
        })
    }
  }, [isPlaying])

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newTime = parseFloat(e.target.value)
    setCurrentTime(newTime)
    if (audioRef.current) {
      audioRef.current.currentTime = newTime
    }
  }

  const handleRestart = () => {
    if (audioRef.current) {
      audioRef.current.currentTime = 0
      setCurrentTime(0)
      if (!isPlaying) {
        audioRef.current.play().then(() => setIsPlaying(true)).catch(() => { })
      }
    }
  }

  const toggleMute = () => {
    setIsMuted((prev) => !prev)
  }

  const cycleSpeed = () => {
    const rates = [1, 1.25, 1.5, 2]
    const nextIdx = (rates.indexOf(playbackRate) + 1) % rates.length
    setPlaybackRate(rates[nextIdx])
  }

  const progressPercent = duration > 0 ? (currentTime / duration) * 100 : 0

  return (
    <div className="audio-preview-bar" role="region" aria-label="Audio preview player">
      { }
      <audio
        ref={audioRef}
        src={currentSourceUrl}
        preload="metadata"
        onTimeUpdate={() => {
          if (audioRef.current) {
            setCurrentTime(audioRef.current.currentTime)
          }
        }}
        onLoadedMetadata={() => {
          if (audioRef.current) {
            setDuration(audioRef.current.duration || 0)
            setIsLoading(false)
          }
        }}
        onWaiting={() => setIsLoading(true)}
        onPlaying={() => {
          setIsLoading(false)
          setIsPlaying(true)
        }}
        onEnded={() => {
          setIsPlaying(false)
          setCurrentTime(0)
        }}
        onError={() => {
          setIsLoading(false)
          setIsPlaying(false)
          setHasError(true)
        }}
      />

      {/* Top Header: Comparison Selector Tabs */}
      <div className="preview-bar-tabs">
        <div className="preview-bar-toggle-group" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeVariant === 'original'}
            className={`preview-tab-btn ${activeVariant === 'original' ? 'is-active' : ''}`}
            onClick={() => handleVariantSwitch('original')}
            disabled={!resolvedOriginal}
            title={originalFileName ? `Original: ${originalFileName}` : 'Audio original'}
          >
            <FileAudio size={13} />
            <span>Audio Original</span>
            {!resolvedOriginal && <span className="tab-pill-na">(N/A)</span>}
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeVariant === 'filtered'}
            className={`preview-tab-btn ${activeVariant === 'filtered' ? 'is-active' : ''}`}
            onClick={() => handleVariantSwitch('filtered')}
            disabled={!resolvedFiltered}
            title={filteredFileName ? `Con Filtro: ${filteredFileName}` : 'Audio filtrado'}
          >
            <Sparkles size={13} className="sparkle-icon" />
            <span>Con Filtro</span>
            {!resolvedFiltered && <span className="tab-pill-na">(...)</span>}
          </button>
        </div>
      </div>

      {/* Active Filter Info Badge */}
      <div className="preview-bar-filter-info">
        {activeVariant === 'filtered' ? (
          <div className="filter-badge filter-badge-active">
            <Sparkles size={12} className="sparkle-icon" />
            <span>
              <strong>Filtro:</strong> Realce de Voz (400Hz-3kHz + Boost +8dB)
            </span>
          </div>
        ) : (
          <div className="filter-badge filter-badge-original">
            <FileAudio size={12} />
            <span>
              <strong>Audio:</strong> Pista original sin procesar
            </span>
          </div>
        )}
      </div>

      {/* Player Section: Timeline on Top, Controls Below */}
      <div className="preview-player-container">
        {/* Timeline Row */}
        <div className="preview-timeline-row">
          <span className="preview-time-display current-time">
            {formatDuration(currentTime)}
          </span>
          <div className="preview-slider-container">
            <input
              type="range"
              min={0}
              max={duration || 100}
              step={0.1}
              value={currentTime}
              onChange={handleSeek}
              className="preview-slider"
              aria-label="Posición de reproducción"
              style={{
                background: `linear-gradient(to right, var(--color-accent) ${progressPercent}%, var(--color-border-subtle) ${progressPercent}%)`,
              }}
            />
          </div>
          <span className="preview-time-display total-time">
            {formatDuration(duration)}
          </span>
        </div>

        {/* Buttons Row */}
        <div className="preview-actions-row">
          <div className="preview-actions-left">
            <button
              type="button"
              className="preview-btn-restart"
              onClick={handleRestart}
              title="Reiniciar reproducción"
              aria-label="Reiniciar reproducción"
            >
              <RotateCcw size={14} />
            </button>

            <button
              type="button"
              className={`preview-btn-play ${isPlaying ? 'is-playing' : ''}`}
              onClick={togglePlay}
              disabled={!currentSourceUrl || hasError}
              title={isPlaying ? 'Pausar' : 'Reproducir'}
              aria-label={isPlaying ? 'Pausar' : 'Reproducir'}
            >
              {isPlaying ? <Pause size={16} /> : <Play size={16} style={{ marginLeft: '2px' }} />}
            </button>

            <button
              type="button"
              className="preview-btn-speed"
              onClick={cycleSpeed}
              title="Velocidad de reproducción"
              aria-label={`Velocidad ${playbackRate}x`}
            >
              {playbackRate}x
            </button>
          </div>

          <div className="preview-actions-right">
            <button
              type="button"
              className="preview-btn-mute"
              onClick={toggleMute}
              title={isMuted ? 'Activar sonido' : 'Silenciar'}
              aria-label={isMuted ? 'Activar sonido' : 'Silenciar'}
            >
              {isMuted || volume === 0 ? <VolumeX size={15} /> : <Volume2 size={15} />}
            </button>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={isMuted ? 0 : volume}
              onChange={(e) => {
                const val = parseFloat(e.target.value)
                setVolume(val)
                if (isMuted && val > 0) setIsMuted(false)
              }}
              className="preview-volume-slider"
              aria-label="Volumen"
            />
          </div>
        </div>
      </div>

      {hasError && (
        <p className="preview-bar-error">
          No se pudo reproducir este audio. Verifica que el servidor esté activo.
        </p>
      )}
      {isLoading && (
        <span className="preview-bar-loading-hint">Cargando audio...</span>
      )}
    </div>
  )
}
