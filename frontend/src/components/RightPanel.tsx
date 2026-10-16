import { useMemo, useState, useEffect, useCallback } from 'react'
import { Copy, Check, Save } from 'lucide-react'
import type { Metric, ReviewResult, PipelineInfo, AgentConfig, OutputType } from '../types'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'

interface Props {
  activeTab: 'json' | 'structure' | 'agent'
  onTabChange: (tab: 'json' | 'structure' | 'agent') => void
  dashboardJson: string | null
  outputType: OutputType
  metrics: Metric[]
  review: ReviewResult | null
  agentLogs: Record<string, Array<{ time: string; message: string; level: string }>>
  selectedAgentRole: string | null
  selectedPipeline: PipelineInfo | null
  agentConfigs: Record<string, AgentConfig>
}

/* ── JSON syntax highlighter ── */
function highlightJson(json: string): Array<{ num: number; html: string }> {
  try {
    const formatted = JSON.stringify(JSON.parse(json), null, 2)
    return formatted.split('\n').map((line, i) => ({
      num: i + 1,
      html: line
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"([^"]+)"(?=\s*:)/g, '<span class="json-key">"$1"</span>')
        .replace(/:\s*"([^"]*)"/g, (_m, v) => `: <span class="json-string">"${v}"</span>`)
        .replace(/^\s*"([^"]*)"(,?)$/gm, (_m, v, comma) => {
          if (_m.includes('json-key') || _m.includes('json-string')) return _m
          return `<span class="json-string">"${v}"</span>${comma}`
        })
        .replace(/:\s*(-?\d+\.?\d*)/g, ': <span class="json-number">$1</span>')
        .replace(/:\s*(true|false)/g, ': <span class="json-bool">$1</span>')
        .replace(/:\s*(null)/g, ': <span class="json-null">$1</span>')
        .replace(/([{}[\]])/g, '<span class="json-bracket">$1</span>')
    }))
  } catch {
    return json.split('\n').map((line, i) => ({ num: i + 1, html: line }))
  }
}

/* ── Dashboard structure ── */
interface TreeNode {
  type: 'row' | 'panel'
  title: string
  panelType?: string
}

function parseDashboardStructure(json: string | null): TreeNode[] {
  if (!json) return []
  try {
    const dash = JSON.parse(json)
    const panels = dash.panels ?? []
    const nodes: TreeNode[] = []
    for (const p of panels) {
      if (p.type === 'row') {
        nodes.push({ type: 'row', title: p.title ?? 'Row' })
        if (p.panels) {
          for (const sub of p.panels) {
            nodes.push({ type: 'panel', title: sub.title ?? 'Panel', panelType: sub.type })
          }
        }
      } else {
        nodes.push({ type: 'panel', title: p.title ?? 'Panel', panelType: p.type })
      }
    }
    return nodes
  } catch {
    return []
  }
}

function panelBadgeClass(type?: string): string {
  if (!type) return ''
  const t = type.toLowerCase()
  if (t === 'timeseries') return 'timeseries'
  if (t === 'stat') return 'stat'
  if (t === 'gauge') return 'gauge'
  return 'stat'
}

function agentColorClass(role: string): string {
  const r = role.toLowerCase()
  if (r.includes('extract')) return 'extractor'
  if (r.includes('archit')) return 'architect'
  if (r.includes('review')) return 'reviewer'
  if (r.includes('valid')) return 'validator'
  return ''
}

/* ── CSV table renderer ── */
function CsvTable({ content }: { content: string }) {
  const rows = content.trim().split('\n').map(r => r.split(','))
  if (rows.length === 0) return null

  return (
    <div style={{ padding: '8px 12px', overflow: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '10px' }}>
        <thead>
          <tr>
            {rows[0].map((h, i) => (
              <th key={i} style={{ padding: '4px 8px', borderBottom: '1px solid var(--border)', color: 'var(--text)', textAlign: 'left', fontWeight: 600 }}>
                {h.trim()}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.slice(1).map((row, ri) => (
            <tr key={ri}>
              {row.map((cell, ci) => (
                <td key={ci} style={{ padding: '3px 8px', borderBottom: '1px solid var(--border-subtle)', color: 'var(--text-secondary)' }}>
                  {cell.trim()}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/* ── Output renderer based on type ── */
function OutputRenderer({ content, outputType }: { content: string; outputType: OutputType }) {
  if (outputType === 'json') {
    const lines = highlightJson(content)
    return (
      <div className="json-preview" style={{ flex: 1, overflowY: 'auto' }}>
        {lines.map(line => (
          <div key={line.num} className="json-line">
            <span className="json-line-num">{line.num}</span>
            <span className="json-line-content" dangerouslySetInnerHTML={{ __html: line.html }} />
          </div>
        ))}
      </div>
    )
  }

  if (outputType === 'html') {
    return (
      <iframe
        srcDoc={content}
        sandbox="allow-scripts"
        style={{ width: '100%', flex: 1, border: 'none', background: '#fff', borderRadius: 'var(--radius)' }}
      />
    )
  }

  if (outputType === 'csv') {
    return <CsvTable content={content} />
  }

  return (
    <pre style={{ padding: '12px 16px', fontSize: '10.5px', color: 'var(--text-secondary)', whiteSpace: 'pre-wrap', wordBreak: 'break-word', lineHeight: 1.6 }}>
      {content}
    </pre>
  )
}

export function RightPanel({
  activeTab, onTabChange, dashboardJson, outputType, metrics, review, agentLogs,
  selectedAgentRole, selectedPipeline, agentConfigs
}: Props) {
  const [isCopied, setIsCopied] = useState(false)

  const [promptContent, setPromptContent] = useState<string | null>(null)
  const [promptOriginal, setPromptOriginal] = useState<string | null>(null)
  const [promptLoading, setPromptLoading] = useState(false)
  const [promptSaving, setPromptSaving] = useState(false)
  const [promptSaved, setPromptSaved] = useState(false)

  const fetchPrompt = useCallback(() => {
    if (!selectedAgentRole || !selectedPipeline) {
      setPromptContent(null)
      setPromptOriginal(null)
      return
    }
    const agentInfo = selectedPipeline.agents.find(a => a.role === selectedAgentRole)
    if (!agentInfo) return

    const promptFile = agentInfo.promptFile
    setPromptLoading(true)
    setPromptSaved(false)
    fetch(`/api/pipelines/${selectedPipeline.name}/prompts/${promptFile}`)
      .then(r => r.json())
      .then(d => {
        setPromptContent(d.content)
        setPromptOriginal(d.content)
        setPromptLoading(false)
      })
      .catch(() => {
        setPromptContent(null)
        setPromptOriginal(null)
        setPromptLoading(false)
      })
  }, [selectedAgentRole, selectedPipeline, agentConfigs])

  useEffect(() => { fetchPrompt() }, [fetchPrompt])

  const handleSavePrompt = async () => {
    if (!selectedAgentRole || !selectedPipeline || promptContent === null) return
    const agentInfo = selectedPipeline.agents.find(a => a.role === selectedAgentRole)
    if (!agentInfo) return

    const promptFile = agentInfo.promptFile
    setPromptSaving(true)
    try {
      await fetch(`/api/pipelines/${selectedPipeline.name}/prompts/${promptFile}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: promptContent })
      })
      setPromptOriginal(promptContent)
      setPromptSaved(true)
      setTimeout(() => setPromptSaved(false), 2000)
    } catch { /* ignore */ }
    setPromptSaving(false)
  }

  const promptDirty = promptContent !== null && promptContent !== promptOriginal

  const structure = useMemo(() => parseDashboardStructure(dashboardJson), [dashboardJson])

  const handleCopy = () => {
    if (dashboardJson) {
      navigator.clipboard.writeText(dashboardJson)
      setIsCopied(true)
      setTimeout(() => setIsCopied(false), 2000)
    }
  }

  const selectedAgentInfo = selectedPipeline?.agents.find(a => a.role === selectedAgentRole)
  const selectedAgentLogs = selectedAgentRole ? agentLogs[
    Object.keys(agentLogs).find(k => k.toLowerCase().includes(selectedAgentRole.toLowerCase().slice(0, 5))) ?? ''
  ] : null

  const outputTabLabel = outputType === 'json' ? 'json' : 'output'

  return (
    <div className="right-panel">
      <Tabs
        value={activeTab}
        onValueChange={(v) => onTabChange(v as 'json' | 'structure' | 'agent')}
        className="flex flex-col h-full"
      >
        <TabsList>
          <TabsTrigger value="json">{outputTabLabel}</TabsTrigger>
          <TabsTrigger value="structure">structure</TabsTrigger>
          <TabsTrigger value="agent">agent</TabsTrigger>
        </TabsList>

        {/* Output tab */}
        <TabsContent value="json">
          {dashboardJson ? (
            <div style={{ position: 'relative', height: '100%', display: 'flex', flexDirection: 'column' }}>
              <button
                onClick={handleCopy}
                style={{
                  position: 'absolute', top: '12px', right: '16px',
                  display: 'flex', alignItems: 'center', gap: '6px',
                  padding: '6px 12px',
                  backgroundColor: 'var(--bg-elevated)',
                  border: `1px solid ${isCopied ? 'var(--success)' : 'var(--border)'}`,
                  borderRadius: 'var(--radius)',
                  color: isCopied ? 'var(--success)' : 'var(--text-secondary)',
                  cursor: 'pointer', fontSize: '11px', fontWeight: 600,
                  transition: 'all 0.2s ease', zIndex: 10,
                  boxShadow: '0 4px 12px rgba(0,0,0,0.2)'
                }}
              >
                {isCopied ? <Check size={14} strokeWidth={2.5} /> : <Copy size={14} strokeWidth={2} />}
                {isCopied ? 'Copied!' : 'Copy'}
              </button>
              <OutputRenderer content={dashboardJson} outputType={outputType} />
            </div>
          ) : (
            <div className="json-empty">No output yet</div>
          )}
        </TabsContent>

        {/* Structure tab */}
        <TabsContent value="structure">
          {structure.length > 0 ? (
            <div className="structure-tree">
              {structure.map((node, i) => (
                <div key={i} className="tree-node">
                  <div className={`tree-row ${node.type === 'row' ? 'row-level' : 'panel-level'}`}>
                    {node.type === 'row' ? (
                      <>
                        <span className="tree-badge row">row</span>
                        <span>{node.title}</span>
                      </>
                    ) : (
                      <>
                        <span className={`tree-badge ${panelBadgeClass(node.panelType)}`}>
                          {node.panelType ?? '?'}
                        </span>
                        <span>{node.title}</span>
                      </>
                    )}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="structure-empty">No structure to display</div>
          )}
        </TabsContent>

        {/* Agent tab */}
        <TabsContent value="agent">
          {selectedAgentRole && selectedAgentInfo ? (
            <div className="agent-tab-content">
              <div className="prompt-editor">
                <div className="prompt-editor-header">
                  <span className="prompt-editor-label">Prompt: {selectedAgentInfo.name}</span>
                  <button
                    className={`btn-save-prompt ${promptSaved ? 'saved' : ''}`}
                    onClick={handleSavePrompt}
                    disabled={!promptDirty || promptSaving}
                  >
                    {promptSaved ? <Check size={12} strokeWidth={2.5} /> : <Save size={12} strokeWidth={2} />}
                    {promptSaving ? 'Saving...' : promptSaved ? 'Saved' : 'Save'}
                  </button>
                </div>
                {promptLoading ? (
                  <div className="prompt-editor-loading">Loading prompt...</div>
                ) : promptContent !== null ? (
                  <textarea
                    className="prompt-editor-textarea"
                    value={promptContent}
                    onChange={e => setPromptContent(e.target.value)}
                    spellCheck={false}
                  />
                ) : (
                  <div className="prompt-editor-loading">Prompt not available</div>
                )}
              </div>

              {selectedAgentLogs && selectedAgentLogs.length > 0 && (
                <div className="agent-io-section">
                  <div className={`agent-io-header ${agentColorClass(selectedAgentRole)}`}>Logs</div>
                  <div className="agent-io-block">
                    <div className="agent-io-content">
                      {selectedAgentLogs.map(l => `${l.time} ${l.message}`).join('\n')}
                    </div>
                  </div>
                </div>
              )}

              {selectedAgentRole.includes('extract') && metrics.length > 0 && (
                <div className="agent-io-section">
                  <div className="agent-io-header extractor">Output: Metrics</div>
                  <div className="agent-io-block">
                    <div className="agent-io-content">
                      {metrics.map(m => `${m.name} (${m.type})`).join('\n')}
                    </div>
                  </div>
                </div>
              )}

              {selectedAgentRole.includes('review') && review && (
                <div className="agent-io-section">
                  <div className="agent-io-header reviewer">Output: Review</div>
                  <div className="agent-io-block">
                    <div className="agent-io-content">
                      {review.issues.map(i => `[${i.severity}] ${i.description}`).join('\n')}
                      {review.suggestions.length > 0 && '\n' + review.suggestions.map(s => `+ ${s}`).join('\n')}
                    </div>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="agent-io-empty">
              {selectedPipeline ? 'Click an agent in the sidebar' : 'No pipeline selected'}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}
