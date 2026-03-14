interface Props { reason: string; onDismiss?: () => void }

export function ActionRejectedToast({ reason, onDismiss }: Props) {
  return (
    <div className="action-rejected-toast" role="alert">
      <span>{reason}</span>
      {onDismiss && <button onClick={onDismiss} aria-label="Dismiss">×</button>}
    </div>
  )
}
