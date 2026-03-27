import { createContext, useCallback, useContext, useRef, useState, ReactNode } from 'react'

interface Toast {
  id: number
  message: string
  variant: 'success' | 'error' | 'info'
}

interface ToastContextValue {
  showToast: (message: string, variant?: Toast['variant']) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

let nextId = 0

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const timersRef = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map())

  const showToast = useCallback((message: string, variant: Toast['variant'] = 'info') => {
    const id = ++nextId
    setToasts((prev: Toast[]) => [...prev, { id, message, variant }])

    const timer = setTimeout(() => {
      setToasts((prev: Toast[]) => prev.filter((t: Toast) => t.id !== id))
      timersRef.current.delete(id)
    }, 3000)
    timersRef.current.set(id, timer)
  }, [])

  function dismiss(id: number) {
    const timer = timersRef.current.get(id)
    if (timer !== undefined) {
      clearTimeout(timer)
      timersRef.current.delete(id)
    }
    setToasts((prev: Toast[]) => prev.filter((t: Toast) => t.id !== id))
  }

  const variantBorderColor: Record<Toast['variant'], string> = {
    success: 'var(--status-success)',
    error:   'var(--neon-magenta)',
    info:    'var(--accent)',
  }

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      {toasts.length > 0 && (
        <div
          aria-live="polite"
          aria-atomic="false"
          style={{
            position: 'fixed',
            bottom: 'var(--space-6)',
            right: 'var(--space-6)',
            zIndex: 999,
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-2)',
            maxWidth: '320px',
            width: 'calc(100vw - 2 * var(--space-6))',
          }}
        >
          {toasts.map((t: Toast) => (
            <div
              key={t.id}
              role="alert"
              style={{
                background: 'var(--surface-overlay)',
                border: `1px solid ${variantBorderColor[t.variant]}`,
                borderRadius: 'var(--radius-md)',
                padding: 'var(--space-3) var(--space-4)',
                fontFamily: 'var(--font-mono)',
                fontSize: '.76rem',
                color: 'var(--text-bright)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 'var(--space-3)',
                animation: 'action-rejected-slide-in var(--dur-slow) var(--ease-out) both',
              }}
            >
              <span style={{ flex: 1, lineHeight: 1.5 }}>{t.message}</span>
              <button
                type="button"
                onClick={() => dismiss(t.id)}
                aria-label="Dismiss notification"
                style={{
                  flexShrink: 0,
                  width: '24px',
                  height: '24px',
                  borderRadius: 'var(--radius-sm)',
                  border: '1px solid var(--edge-strong)',
                  background: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                  fontSize: '1rem',
                  lineHeight: 1,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                &times;
              </button>
            </div>
          ))}
        </div>
      )}
    </ToastContext.Provider>
  )
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used within ToastProvider')
  return ctx
}
