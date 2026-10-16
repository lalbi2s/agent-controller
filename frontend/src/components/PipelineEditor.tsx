import { useState, useEffect } from 'react'
import { Plus, Trash2, Save, ChevronUp, ChevronDown } from 'lucide-react'
import type { PipelineInfo } from '../types'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogBody, DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface AgentDraft {
  id: string
  role: string
  name: string
  description: string
  prompt: string
}

interface Props {
  pipeline: PipelineInfo
  onClose: () => void
  onSaved: () => void
}

export function PipelineEditor({ pipeline, onClose, onSaved }: Props) {
  const [displayName, setDisplayName] = useState(pipeline.displayName)
  const [description, setDescription] = useState(pipeline.description)
  const [outputType, setOutputType] = useState(pipeline.outputType)
  const [maxCorrectionLoops, setMaxCorrectionLoops] = useState(pipeline.maxCorrectionLoops)
  const [agents, setAgents] = useState<AgentDraft[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = async () => {
      const drafts = await Promise.all(
        pipeline.agents.map(async a => {
          let prompt = ''
          try {
            const res = await fetch(`/api/pipelines/${pipeline.name}/prompts/${a.promptFile}`)
            if (res.ok) {
              const data = await res.json()
              prompt = data.content ?? ''
            }
          } catch { /* keep empty */ }
          return {
            id: `${a.role}-${a.step}`,
            role: a.role,
            name: a.name,
            description: a.description ?? '',
            prompt,
          }
        })
      )
      setAgents(drafts)
      setLoading(false)
    }
    load()
  }, [pipeline.name])

  const update = (id: string, patch: Partial<AgentDraft>) =>
    setAgents(prev => prev.map(a => a.id === id ? { ...a, ...patch } : a))

  const remove = (id: string) =>
    setAgents(prev => prev.filter(a => a.id !== id))

  const addAgent = () =>
    setAgents(prev => [...prev, { id: Date.now().toString(), role: '', name: '', description: '', prompt: '' }])

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
      const res = await fetch(`/api/pipelines/${pipeline.name}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: pipeline.name,
          displayName: displayName.trim(),
          description: description.trim(),
          outputType,
          maxCorrectionLoops,
          agents: agents.map(a => ({
            name: a.name.trim(),
            description: a.description.trim(),
            prompt: a.prompt || `# ${a.name}\n\nPrompt for ${a.name}.`,
          })),
        }),
      })

      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        throw new Error((data as { error?: string }).error || `Error ${res.status}`)
      }

      onSaved()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    }
    setSaving(false)
  }

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-[620px]">
        <DialogHeader>
          <DialogTitle>Edit — {pipeline.displayName}</DialogTitle>
        </DialogHeader>

        {loading ? (
          <DialogBody>
            <div className="pc-empty py-8">Loading prompts...</div>
          </DialogBody>
        ) : (
          <>
            <DialogBody>
              {/* Name */}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="pe-name">Name</Label>
                <Input
                  id="pe-name"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  placeholder="Display name"
                />
                <span className="text-[9px] text-[var(--text-muted)] italic">{pipeline.name}</span>
              </div>

              {/* Description */}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="pe-desc">Description</Label>
                <Input
                  id="pe-desc"
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                  placeholder="Optional description"
                />
              </div>

              {/* Output type + Max loops */}
              <div className="flex gap-3">
                <div className="flex flex-col gap-1.5 flex-1">
                  <Label htmlFor="pe-output">Output type</Label>
                  <div className="select-wrap">
                    <select id="pe-output" value={outputType} onChange={e => setOutputType(e.target.value as typeof outputType)}>
                      <option value="text">Text</option>
                      <option value="json">JSON</option>
                      <option value="html">HTML</option>
                      <option value="csv">CSV</option>
                      <option value="markdown">Markdown</option>
                    </select>
                  </div>
                </div>

                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="pe-loops">Max loops</Label>
                  <Input
                    id="pe-loops"
                    type="number"
                    min={0}
                    max={10}
                    value={maxCorrectionLoops}
                    onChange={e => setMaxCorrectionLoops(Number(e.target.value))}
                    className="w-16"
                  />
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
                      <div className="flex flex-col gap-px items-center">
                        <button className="pc-btn-order" onClick={() => moveUp(idx)} disabled={idx === 0} title="Move up">
                          <ChevronUp size={11} strokeWidth={2} />
                        </button>
                        <span className="pc-agent-step">{idx + 1}</span>
                        <button className="pc-btn-order" onClick={() => moveDown(idx)} disabled={idx === agents.length - 1} title="Move down">
                          <ChevronDown size={11} strokeWidth={2} />
                        </button>
                      </div>

                      <div className="flex flex-1 gap-1">
                        <Input
                          className="pc-input-sm"
                          value={agent.name}
                          onChange={e => update(agent.id, { name: e.target.value })}
                          placeholder="Name (Ex: Extractor)"
                        />
                        <Input
                          className="pc-input-sm"
                          value={agent.description}
                          onChange={e => update(agent.id, { description: e.target.value })}
                          placeholder="Description (optional)"
                        />
                      </div>

                      <Button variant="danger" size="icon" onClick={() => remove(agent.id)} className="flex-shrink-0 h-6 w-6">
                        <Trash2 size={11} strokeWidth={2} />
                      </Button>
                    </div>

                    <Textarea
                      value={agent.prompt}
                      onChange={e => update(agent.id, { prompt: e.target.value })}
                      placeholder="Agent prompt (markdown)..."
                      rows={4}
                      className="mt-1"
                    />
                  </div>
                ))}

                {agents.length === 0 && (
                  <div className="pc-empty">Add at least one agent to continue</div>
                )}
              </div>

              {error && <div className="pc-error">{error}</div>}
            </DialogBody>

            <DialogFooter>
              <Button
                variant="save"
                size="lg"
                onClick={handleSave}
                disabled={!displayName.trim() || agents.length === 0 || saving}
              >
                <Save size={12} strokeWidth={2} />
                {saving ? 'Saving...' : 'Save changes'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
