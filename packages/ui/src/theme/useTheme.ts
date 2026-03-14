import { useState, useCallback } from 'react'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'meepliton-theme'

function readCurrentTheme(): Theme {
  // The inline script in index.html already set data-theme on <html> before
  // React hydrated — read it back so initial state matches with no flash.
  const attr = document.documentElement.getAttribute('data-theme')
  return attr === 'light' ? 'light' : 'dark'
}

export function useTheme(): { theme: Theme; toggleTheme: () => void } {
  const [theme, setTheme] = useState<Theme>(readCurrentTheme)

  const toggleTheme = useCallback(() => {
    setTheme(prev => {
      const next: Theme = prev === 'dark' ? 'light' : 'dark'
      document.documentElement.setAttribute('data-theme', next)
      localStorage.setItem(STORAGE_KEY, next)
      return next
    })
  }, [])

  return { theme, toggleTheme }
}
