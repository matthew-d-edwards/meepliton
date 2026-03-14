import { useState } from 'react'

interface Props { code: string }

export function JoinCodeDisplay({ code }: Props) {
  const [copied, setCopied] = useState(false)

  function copy() {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    })
  }

  return (
    <div className="join-code">
      <span className="join-code__value">{code}</span>
      <button className="join-code__copy" onClick={copy}>
        {copied ? 'Copied!' : 'Copy'}
      </button>
    </div>
  )
}
