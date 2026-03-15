import { useEffect, useRef } from 'react'

interface Props {
  reason: string
  onDismiss: () => void
  /** Auto-dismiss delay in milliseconds. Defaults to 4000. */
  dismissAfterMs?: number
}

export function ActionRejectedToast({ reason, onDismiss, dismissAfterMs = 4000 }: Props) {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    timerRef.current = setTimeout(onDismiss, dismissAfterMs)
    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current)
    }
  }, [reason, onDismiss, dismissAfterMs])

  return (
    <div
      className="action-rejected-toast"
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
    >
      <span className="action-rejected-toast__message">{reason}</span>
      <button
        className="action-rejected-toast__close"
        onClick={onDismiss}
        aria-label="Dismiss notification"
        type="button"
      >
        ×
      </button>
    </div>
  )
}
