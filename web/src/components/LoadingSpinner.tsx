import './LoadingSpinner.css'

interface LoadingSpinnerProps {
  size?: 'sm' | 'md'
}

export function LoadingSpinner({ size = 'md' }: LoadingSpinnerProps) {
  return (
    <div
      className={`loading-spinner loading-spinner-${size}`}
      aria-hidden="true"
    />
  )
}
