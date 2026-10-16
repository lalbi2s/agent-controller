import { useState } from 'react'
import { Plus, Trash2, Save, ChevronUp, ChevronDown } from 'lucide-react'
import type { ModelInfo } from '../types'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogBody, DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface AgentDraft {
  id: string
  name: string
  description: string
  prompt: string
  provider: string
  model: string
}

interface Props {
  models: ModelInfo[]
  onClose: () => void
  onCreated: () => void
}

const PROVIDERS = ['anthropic', 'github'] as const

function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/[àáâãäå]/g, 'a')
    .replace(/[èéêë]/g, 'e')
    .replace(/[ìíîï]/g, 'i')
    .replace(/[òóôõö]/g, 'o')
    .replace(/[ùúûü]/g, 'u')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
}

export function PipelineCreator({ models, onClose, onCreated }: Props) {
  const [displayName, setDisplayName] = useState('')
  const [description, setDescription] = useState('')
  const [outputType, setOutputType] = useState('text')
  const [agents, setAgents] = useState<AgentDraft[]>([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const name = slugify(displayName)

  const defaultModelForProvider = (provider: string) =>
    models.find(m => m.provider === provider)?.id ?? ''

  const addAgent = () => {
    const defaultProvider = 'anthropic'
    setAgents(prev => [...prev, {
      id: Date.now().toString(),
      name: '',
      description: '',
      prompt: '',
      provider: defaultProvider,
      model: defaultModelForProvider(defaultProvider),
    }])
  }

  const updateAgent = (id: string, update: Partial<AgentDraft>) => {
    setAgents(prev => prev.map(a => a.id === id ? { ...a, ...update } : a))
  }

  const handleProviderChange = (id: string, provider: string) => {
    updateAgent(id, { provider, model: defaultModelForProvider(provider) })
  }

  const removeAgent = (id: string) => {
    setAgents(prev => prev.filter(a => a.id !== id))
  }

  const moveUp = (idx: number) => {
    if (idx === 0) return
    setAgents(prev => {
      const next = [...prev];
      [next[idx - 1], next[idx]] = [next[idx], next[idx - 1]]
      return next
    })
  }

  const moveDown = (idx: number) => {
    setAgents(prev => {
      if (idx >= prev.length - 1) return prev
      const next = [...prev];
      [next[idx], next[idx + 1]] = [next[idx + 1], next[idx]]
      return next
    })
  }

  const handleSave = async () => {
    if (!displayName.trim() || agents.length === 0) return
    for (const a of agents) {
      if (!a.name.trim()) { setError('Each agent must have a name'); return }
    }

    setSaving(true)
    setError(null)

    try {
      const res = await fetch('/api/pipelines', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          displayName: displayName.trim(),
          description: description.trim(),
          outputType,
          maxCorrectionLoops: 2,
          agents: agents.map(a => ({
            name: a.name.trim(),
            description: a.description.trim(),
            prompt: a.prompt || `# ${a.name}\n\nPrompt for agent ${a.name}.`,
          }))
        })
      })

      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        throw new Error((data as { error?: string }).error || `Error ${res.status}`)
      }

      onCreated()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    }
    setSaving(false)
  }

  const canSave = displayName.trim().length > 0 && agents.length > 0 && !saving

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-[620px]">
        <DialogHeader>
          <DialogTitle>New Pipeline</DialogTitle>
        </DialogHeader>

        <DialogBody>
          {/* Name */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="pc-name">Name</Label>
            <Input
              id="pc-name"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              placeholder="Ex: Log Analyzer"
              autoFocus
            />
            {name && <span className="text-[9px] text-[var(--text-muted)] italic">{name}</span>}
          </div>

          {/* Description */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="pc-desc">Description</Label>
            <Input
              id="pc-desc"
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Optional description"
            />
          </div>

          {/* Output type */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="pc-output">Output type</Label>
            <div className="select-wrap">
              <select id="pc-output" value={outputType} onChange={e => setOutputType(e.target.value)}>
                <option value="text">Text</option>
                <option value="json">JSON</option>
                <option value="html">HTML</option>
                <option value="csv">CSV</option>
                <option value="markdown">Markdown</option>
              </select>
            </div>
          </div>

          {/* Agents header */}
          <div className="flex items-center justify-between mt-1">
            <Label>Agents ({agents.length})</Label>
            <Button variant="add" size="sm" onClick={addAgent}>
              <Plus size={11} strokeWidth={2} /> Agent
            </Button>
          </div>

          {/* Agents list */}
          <div className="flex flex-col gap-2">
            {agents.map((agent, idx) => (
              <div key={agent.id} className="pc-agent-card">
                <div className="pc-agent-top">
                  {/* Order controls */}
                  <div className="flex flex-col gap-px items-center">
                    <button className="pc-btn-order" onClick={() => moveUp(idx)} disabled={idx === 0} title="Move up">
                      <ChevronUp size={11} strokeWidth={2} />
                    </button>
                    <span className="pc-agent-step">{idx + 1}</span>
                    <button className="pc-btn-order" onClick={() => moveDown(idx)} disabled={idx === agents.length - 1} title="Move down">
                      <ChevronDown size={11} strokeWidth={2} />
                    </button>
                  </div>

                  {/* Fields */}
                  <div className="flex flex-1 gap-1">
                    <Input
                      className="pc-input-sm"
                      value={agent.name}
                      onChange={e => updateAgent(agent.id, { name: e.target.value })}
                      placeholder="Name (Ex: Extractor)"
                    />
                    <Input
                      className="pc-input-sm"
                      value={agent.description}
                      onChange={e => updateAgent(agent.id, { description: e.target.value })}
                      placeholder="Description (optional)"
                    />
                  </div>

                  <Button variant="danger" size="icon" onClick={() => removeAgent(agent.id)} className="flex-shrink-0 h-6 w-6">
                    <Trash2 size={11} strokeWidth={2} />
                  </Button>
                </div>

                {/* Provider + Model */}
                <div className="flex gap-1.5 mt-1">
                  <div className="select-wrap flex-1">
                    <select value={agent.provider} onChange={e => handleProviderChange(agent.id, e.target.value)}>
                      {PROVIDERS.map(p => <option key={p} value={p}>{p}</option>)}
                    </select>
                  </div>
                  <div className="select-wrap flex-1">
                    <select value={agent.model} onChange={e => updateAgent(agent.id, { model: e.target.value })}>
                      {models.filter(m => m.provider === agent.provider).map(m => (
                        <option key={m.id} value={m.id}>{m.name}</option>
                      ))}
                    </select>
                  </div>
                </div>

                <Textarea
                  value={agent.prompt}
                  onChange={e => updateAgent(agent.id, { prompt: e.target.value })}
                  placeholder="Agent prompt (markdown)..."
                  rows={3}
                  className="mt-1"
                />
              </div>
            ))}

            {agents.length === 0 && (
              <div className="pc-empty">Add at least one agent to continue</div>
            )}
          </div>

          {error && (
            <div className="pc-error">{error}</div>
          )}
        </DialogBody>

        <DialogFooter>
          <Button variant="save" size="lg" onClick={handleSave} disabled={!canSave}>
            <Save size={12} strokeWidth={2} />
            {saving ? 'Saving...' : 'Save Pipeline'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
