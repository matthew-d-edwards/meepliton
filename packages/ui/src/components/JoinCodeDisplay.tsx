import { useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'

interface Props { code: string }

export function JoinCodeDisplay({ code }: Props) {
  const [codeCopied, setCodeCopied] = useState(false)
  const [urlCopied, setUrlCopied] = useState(false)

  const joinUrl = `${window.location.origin}/join/${code}`

  function copyCode() {
    navigator.clipboard.writeText(code).then(() => {
      setCodeCopied(true)
      setTimeout(() => setCodeCopied(false), 2000)
    })
  }

  function copyUrl() {
    navigator.clipboard.writeText(joinUrl).then(() => {
      setUrlCopied(true)
      setTimeout(() => setUrlCopied(false), 2000)
    })
  }

  return (
    <div className="join-code">
      <div className="join-code__code-row">
        <span className="join-code__value">{code}</span>
        <button className="join-code__copy" onClick={copyCode}>
          {codeCopied ? 'Copied!' : 'Copy code'}
        </button>
      </div>
      <div className="join-code__url-row">
        <a
          className="join-code__url"
          href={joinUrl}
          target="_blank"
          rel="noopener noreferrer"
        >
          {joinUrl}
        </a>
        <button className="join-code__copy" onClick={copyUrl}>
          {urlCopied ? 'Copied!' : 'Copy link'}
        </button>
      </div>
      <div className="join-code__qr">
        <QRCodeSVG
          value={joinUrl}
          size={160}
          bgColor="transparent"
          fgColor="var(--text-primary)"
        />
      </div>
    </div>
  )
}
