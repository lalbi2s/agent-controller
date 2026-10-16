import { useCallback, useEffect, useState } from 'react'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogBody,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface KeyStatus {
  provider: string
  configured: boolean
  source: 'ui' | 'config' | 'none'
  last4: string
}

interface TestResult {
  ok: boolean
  message: string
}

const PROVIDER_LABELS: Record<string, string> = {
  anthropic: 'Anthropic',
  openai: 'OpenAI compatible',
}

const SOURCE_LABELS = { ui: 'saved here', config: 'from configuration', none: '' }

interface CardProps {
  status: KeyStatus
  onChange: () => void
}

function ProviderCard({ status, onChange }: CardProps) {
  const [key, setKey] = useState('')
  const [message, setMessage] = useState<TestResult | null>(null)
  const [busy, setBusy] = useState(false)

  const call = async (path: string, method: string, body?: unknown) => {
    setBusy(true)
    try {
      return await fetch(`/api/keys/${status.provider}${path}`, {
        method,
        headers: body ? { 'Content-Type': 'application/json' } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      })
    } finally {
      setBusy(false)
    }
  }

  const handleSave = async () => {
    const res = await call('', 'PUT', { key })
    setKey('')
    setMessage(res.ok ? { ok: true, message: 'Saved' } : { ok: false, message: 'The key could not be saved' })
    onChange()
  }

  const handleTest = async () => {
    const res = await call('/test', 'POST')
    setMessage(res.ok ? await res.json() : { ok: false, message: 'The test could not run' })
  }

  const handleDelete = async () => {
    await call('', 'DELETE')
    setMessage(null)
    onChange()
  }

  return (
    <div className="pc-agent-card">
      <div className="flex items-center justify-between">
        <Label htmlFor={`key-${status.provider}`}>{PROVIDER_LABELS[status.provider] ?? status.provider}</Label>
        <span className="text-[10px] text-[var(--text-muted)]">
          {status.configured
            ? `Configured ${status.last4 ? `(••••${status.last4}, ` : '('}${SOURCE_LABELS[status.source]})`
            : 'Not configured'}
        </span>
      </div>

      <Input
        id={`key-${status.provider}`}
        type="password"
        autoComplete="off"
        value={key}
        onChange={e => setKey(e.target.value)}
        placeholder="Paste a new API key"
        className="mt-1"
      />

      <div className="flex gap-1.5 mt-1.5 items-center">
        <Button variant="save" size="sm" onClick={handleSave} disabled={busy || key.trim() === ''}>Save</Button>
        <Button variant="add" size="sm" onClick={handleTest} disabled={busy || !status.configured}>Test</Button>
        <Button variant="danger" size="sm" onClick={handleDelete} disabled={busy || status.source !== 'ui'}>Delete</Button>
        {message && (
          <span className="text-[10px]" style={{ color: message.ok ? 'var(--success)' : 'var(--error)' }}>
            {message.message}
          </span>
        )}
      </div>
    </div>
  )
}

export function ApiKeysPage({ onClose }: { onClose: () => void }) {
  const [statuses, setStatuses] = useState<KeyStatus[]>([])

  const refresh = useCallback(() => {
    fetch('/api/keys')
      .then(r => r.json())
      .then(setStatuses)
      .catch(console.error)
  }, [])

  useEffect(() => { refresh() }, [refresh])

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-[520px]">
        <DialogHeader>
          <DialogTitle>API keys</DialogTitle>
        </DialogHeader>
        <DialogBody>
          <p className="text-[10px] text-[var(--text-muted)]">
            A key saved here is stored encrypted on this computer and takes priority over the configuration.
            It is never sent back to the browser.
          </p>
          {statuses.map(s => <ProviderCard key={s.provider} status={s} onChange={refresh} />)}
        </DialogBody>
      </DialogContent>
    </Dialog>
  )
}
