import { useState, useRef, useCallback, useEffect } from 'react'
import './App.css'
import { usePipelines, useModels, useAgentDefaults } from './hooks/useApi'
import type { AgentConfig, PipelineInfo, CompletionData, AppState, Metric, ReviewResult, OutputType } from './types'
import { Sidebar } from './components/Sidebar'
import { Timeline } from './components/Timeline'
import { ProgressBar } from './components/ProgressBar'
import { LogViewer } from './components/LogViewer'
import { RightPanel } from './components/RightPanel'

interface LogEntry {
  time: string
  agent: string
  message: string
  level: string
}

interface HistoryEntry {
  id: string
  filename: string
  status: 'complete' | 'error'
  time: string
}

const loadSavedState = () => {
  try {
    const saved = localStorage.getItem('agentControllerState')
    return saved ? JSON.parse(saved) : null
  } catch {
    return null
  }
}

const GRAFANA_URL = (import.meta.env.VITE_GRAFANA_URL as string | undefined)?.replace(/\/$/, '')

export default function App() {
  const { pipelines, loading: pipelinesLoading, refresh: refreshPipelines } = usePipelines()
  const models = useModels()
  const agentDefaults = useAgentDefaults()

  const savedState = loadSavedState()

  const [files, setFiles] = useState<File[]>([])

  const [selectedPipelineName, setSelectedPipelineName] = useState<string | null>(() => savedState?.selectedPipelineName || null)
  const [selectedPipeline, setSelectedPipeline] = useState<PipelineInfo | null>(null)
  const [agentConfigs, setAgentConfigs] = useState<Record<string, AgentConfig>>(() => savedState?.agentConfigs || {})
  const [history, setHistory] = useState<HistoryEntry[]>(() => savedState?.history || [])
  const [result, setResult] = useState<CompletionData | null>(null)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [metrics, setMetrics] = useState<Metric[]>([])
  const [agentStatuses, setAgentStatuses] = useState<Record<string, string>>({})
  const [agentTimings, setAgentTimings] = useState<Record<string, { startMs: number; endMs?: number }>>({})
  const [rightTab, setRightTab] = useState<'json' | 'structure' | 'agent'>(() => savedState?.rightTab || 'json')

  const [appState, setAppState] = useState<AppState>('idle')

  const [breakpoints, setBreakpoints] = useState<Record<string, boolean>>(() => savedState?.breakpoints || {})
  const [pausedRunId, setPausedRunId] = useState<string | null>(null)
  const [selectedAgentRole, setSelectedAgentRole] = useState<string | null>(null)

  const [review, setReview] = useState<ReviewResult | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const abortControllerRef = useRef<AbortController | null>(null)

  useEffect(() => {
    if (pipelines.length > 0 && selectedPipelineName && !selectedPipeline) {
      const p = pipelines.find(pp => pp.name === selectedPipelineName) ?? null
      if (p) setSelectedPipeline(p)
    }
  }, [pipelines, selectedPipelineName, selectedPipeline])

  const agentLogs: Record<string, LogEntry[]> = {}
  logs.forEach(l => {
    if (!agentLogs[l.agent]) agentLogs[l.agent] = []
    agentLogs[l.agent].push(l)
  })

  // Only the settings are kept between visits: never files, results or logs
  useEffect(() => {
    try {
      localStorage.setItem('agentControllerState', JSON.stringify({
        selectedPipelineName, agentConfigs, history, rightTab, breakpoints
      }))
    } catch { /* storage unavailable */ }
  }, [selectedPipelineName, agentConfigs, history, rightTab, breakpoints])

  const outputType: OutputType = selectedPipeline?.outputType ?? 'text'

  const handlePipelineDelete = async (name: string) => {
    try {
      await fetch(`/api/pipelines/${name}`, { method: 'DELETE' })
      refreshPipelines()
      if (selectedPipelineName === name) {
        setSelectedPipeline(null)
        setSelectedPipelineName(null)
        setAgentConfigs({})
      }
    } catch { /* ignore */ }
  }

  const handlePipelineChange = (name: string) => {
    const p = pipelines.find(pp => pp.name === name) ?? null
    setSelectedPipeline(p)
    setSelectedPipelineName(name)
    if (p) {
      const configs: Record<string, AgentConfig> = {}
      p.agents.forEach(agent => {
        configs[agent.role] = agentConfigs[agent.role] || {
          role: agent.role,
          model: agentDefaults[agent.role]?.model || (models.length > 0 ? models[0].id : ''),
          provider: agentDefaults[agent.role]?.provider || 'anthropic',
          enabled: true
        }
      })
      setAgentConfigs(configs)
    }
  }
  // ----------------------------------------------------------------------------------------------

  const handleFileDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
    const droppedFiles = Array.from(e.dataTransfer.files)
    if (droppedFiles.length > 0) setFiles(prev => [...prev, ...droppedFiles])
  }, [])

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files ?? [])
    if (selected.length > 0) setFiles(prev => [...prev, ...selected])
  }

  const handleReset = () => {
    setFiles([])
    setAppState('idle')
    setLogs([])
    setResult(null)
    setAgentStatuses({})
    setAgentTimings({})
    setMetrics([])
    setReview(null)
    setSelectedPipelineName(null)
    setSelectedPipeline(null)
  }

  const getDownloadExtension = (): string => {
    const map: Record<string, string> = { json: '.json', html: '.html', csv: '.csv', markdown: '.md', text: '.txt' }
    return map[outputType] ?? '.txt'
  }

  const handleDownload = () => {
    if (!result?.output) return
    const mimeMap: Record<string, string> = {
      json: 'application/json', html: 'text/html', csv: 'text/csv',
      markdown: 'text/markdown', text: 'text/plain'
    }
    const blob = new Blob([result.output], { type: mimeMap[outputType] ?? 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const baseName = files.length === 1
      ? files[0].name.replace(/\.[^.]+$/, '')
      : 'output'
    a.download = `${baseName}-output${getDownloadExtension()}`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleCopyLink = () => {
    if (!result?.output) return
    try {
      const dash = JSON.parse(result.output)
      const uid = dash.uid ?? 'dashboard'
      const link = `${GRAFANA_URL}/d/${uid}?from=now-1h&to=now`
      navigator.clipboard.writeText(link)
    } catch { /* ignore */ }
  }

  const handleStop = () => {
    abortControllerRef.current?.abort()
    abortControllerRef.current = null
    setAppState('idle')
    setPausedRunId(null)
    setAgentStatuses(prev => {
      const next = { ...prev }
      for (const key of Object.keys(next)) {
        if (next[key] === 'running') next[key] = 'error'
      }
      return next
    })
    setAgentTimings(prev => {
      const next = { ...prev }
      for (const key of Object.keys(next)) {
        if (!next[key].endMs) next[key] = { ...next[key], endMs: Date.now() }
      }
      return next
    })
    setLogs(prev => [...prev, {
      time: new Date().toLocaleTimeString(),
      agent: 'System',
      message: 'Pipeline stopped by user',
      level: 'error'
    }])
  }

  const handleResume = async () => {
    if (!pausedRunId) return
    setAppState('running')
    setPausedRunId(null)
    setAgentStatuses(prev => {
      const next = { ...prev }
      for (const key of Object.keys(next)) {
        if (next[key] === 'paused') next[key] = 'idle'
      }
      return next
    })
    await fetch(`/api/run/resume/${pausedRunId}`, { method: 'POST' })
  }

  const handleGenerate = async () => {
    if (files.length === 0 || !selectedPipeline) return

    const controller = new AbortController()
    abortControllerRef.current = controller

    setAppState('running')
    setLogs([])
    setAgentStatuses({})
    setAgentTimings({})
    setResult(null)
    setMetrics([])
    setReview(null)
    setPausedRunId(null)

    const formData = new FormData()
    files.forEach(f => formData.append('files', f))
    formData.append('pipeline', selectedPipeline.name)
    formData.append('agentConfigs', JSON.stringify(Object.values(agentConfigs)))

    const activeBreakpoints = Object.entries(breakpoints).filter(([, v]) => v).map(([k]) => k)
    if (activeBreakpoints.length > 0) {
      formData.append('breakpoints', JSON.stringify(activeBreakpoints))
    }

    try {
      const response = await fetch('/api/run', { method: 'POST', body: formData, signal: controller.signal })
      if (!response.body) return
      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (!line.startsWith('data: ')) continue
          try {
            const evt = JSON.parse(line.slice(6))
            const time = new Date().toLocaleTimeString()

            if (evt.agent) {
              if (evt.type === 'AgentStart') {
                setAgentStatuses(prev => ({ ...prev, [evt.agent]: 'running' }))
                setAgentTimings(prev => ({ ...prev, [evt.agent]: { startMs: Date.now() } }))
              } else if (evt.type === 'AgentComplete') {
                setAgentStatuses(prev => ({ ...prev, [evt.agent]: 'done' }))
                setAgentTimings(prev => ({ ...prev, [evt.agent]: { ...prev[evt.agent], endMs: Date.now() } }))
              } else if (evt.type === 'AgentError') {
                setAgentStatuses(prev => ({ ...prev, [evt.agent]: 'error' }))
                setAgentTimings(prev => ({ ...prev, [evt.agent]: { ...prev[evt.agent], endMs: Date.now() } }))
              }
            }

            if (evt.type === 'Breakpoint' && evt.data) {
              const bp = evt.data as { runId: string; role: string }
              setAppState('paused')
              setPausedRunId(bp.runId)
              setSelectedAgentRole(bp.role)
              setRightTab('agent')
              if (evt.agent) setAgentStatuses(prev => ({ ...prev, [evt.agent!]: 'paused' }))
            }

            if (evt.type === 'Metrics' && evt.data) setMetrics((evt.data as { metrics: Metric[] }).metrics)
            if (evt.type === 'Review' && evt.data) setReview(evt.data as ReviewResult)

            if (evt.message) {
              setLogs(prev => [...prev, { time, agent: evt.agent ?? 'System', message: evt.message, level: evt.type === 'Error' ? 'error' : 'info' }])
            }

            if (evt.type === 'Complete') {
              setResult(evt.data)
              setAppState('complete')
              const fname = files.length === 1 ? files[0].name : `${files.length} files`
              setHistory(prev => [{ id: Date.now().toString(), filename: fname, status: 'complete' as const, time }, ...prev].slice(0, 5))
            }

            if (evt.type === 'Error' && !evt.agent) {
              setAppState('error')
              const fname = files.length === 1 ? files[0].name : `${files.length} files`
              setHistory(prev => [{ id: Date.now().toString(), filename: fname, status: 'error' as const, time }, ...prev].slice(0, 5))
            }
          } catch { /* skip malformed SSE */ }
        }
      }

      if (appState === 'running') setAppState(prev => prev === 'running' ? 'error' : prev)
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') return
      setAppState('error')
    } finally {
      abortControllerRef.current = null
    }
  }

  const agents = selectedPipeline?.agents ?? []
  const stats = result?.stats
  const hasResult = appState === 'complete' && result

  return (
    <div className="app">
      <Sidebar
        pipelines={pipelines}
        pipelinesLoading={pipelinesLoading}
        selectedPipeline={selectedPipeline}
        onPipelineChange={handlePipelineChange}
        onPipelineCreated={refreshPipelines}
        onPipelineDelete={handlePipelineDelete}
        agentConfigs={agentConfigs}
        models={models}
        onAgentConfigChange={(role, update) =>
          setAgentConfigs(prev => ({ ...prev, [role]: { ...prev[role], ...update } }))
        }
        agentStatuses={agentStatuses}
        agentTimings={agentTimings}
        disabled={appState === 'running' || appState === 'paused'}
        breakpoints={breakpoints}
        onBreakpointToggle={(role) => setBreakpoints(prev => ({ ...prev, [role]: !prev[role] }))}
        selectedAgentRole={selectedAgentRole}
        onAgentSelect={(role) => { setSelectedAgentRole(role); setRightTab('agent') }}
        history={history}
      />

      <main className="center">
        <div className="topbar">
          <div
            className={`drop-zone ${dragOver ? 'drag-over' : ''} ${files.length > 0 ? 'has-file' : ''}`}
            onClick={() => { if (files.length === 0) fileInputRef.current?.click() }}
            onDragOver={e => { e.preventDefault(); setDragOver(true) }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleFileDrop}
          >
            {files.length > 0 ? (
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '10px' }}>
                <span className="drop-zone-icon" style={{ color: 'var(--success)' }}>&#10003;</span>
                <span className="drop-zone-text">
                  {files.length === 1 ? files[0].name : `${files.length} files selected`}
                </span>
                <button
                  className="btn-remove-file"
                  onClick={(e) => { e.stopPropagation(); setFiles([]); if (fileInputRef.current) fileInputRef.current.value = '' }}
                >
                  &#10005; Remove files
                </button>
              </div>
            ) : (
              <>
                <span className="drop-zone-icon">+</span>
                <span className="drop-zone-text">Drop files or click to browse</span>
              </>
            )}
          </div>
          <input
            ref={fileInputRef}
            type="file"
            hidden
            multiple
            onChange={handleFileInput}
          />

          {appState === 'paused' ? (
            <div style={{ display: 'flex', gap: '8px', width: '100%' }}>
              <button className="btn-generate btn-resume" onClick={handleResume} style={{ flex: 1 }}>Continue</button>
              <button className="btn-generate btn-stop" onClick={handleStop} style={{ flex: 0, minWidth: '100px' }}>Stop</button>
            </div>
          ) : appState === 'running' ? (
            <button className="btn-generate btn-stop" onClick={handleStop}>Stop</button>
          ) : (
            <button className="btn-generate" disabled={files.length === 0 || !selectedPipeline} onClick={handleGenerate}>Generate</button>
          )}
        </div>

        {agents.length > 0 && <Timeline agents={agents} timings={agentTimings} statuses={agentStatuses} />}
        {agents.length > 0 && <ProgressBar agents={agents} statuses={agentStatuses} />}

        <LogViewer logs={logs} />

        <div className="stats-row">
          <div className="stat-card">
            <div className="stat-card-value">{(stats?.metricsCount ?? metrics.length) || '-'}</div>
            <div className="stat-card-label">Metrics</div>
          </div>
          <div className="stat-card">
            <div className="stat-card-value">{stats?.outputSize ? `${(stats.outputSize / 1024).toFixed(1)}kb` : '-'}</div>
            <div className="stat-card-label">Output size</div>
          </div>
          <div className="stat-card">
            <div className="stat-card-value">{stats?.totalDurationMs ? `${(stats.totalDurationMs / 1000).toFixed(1)}s` : '-'}</div>
            <div className="stat-card-label">Duration</div>
          </div>
          <div className="stat-card">
            <div className="stat-card-value">{stats?.correctionLoops ?? '-'}</div>
            <div className="stat-card-label">Loops</div>
          </div>
        </div>

        <div className="actions-bar">
          <button className="btn-action primary" disabled={!hasResult} onClick={handleDownload}>
            &#8595; Download {outputType.toUpperCase()}
          </button>
          {outputType === 'json' && GRAFANA_URL && (
            <button className="btn-action" disabled={!hasResult} onClick={handleCopyLink}>
              &#9671; Copy Grafana Link
            </button>
          )}
          <button className="btn-action" onClick={handleReset}>
            &#8635; Reset
          </button>
        </div>
      </main>

      <RightPanel
        activeTab={rightTab}
        onTabChange={setRightTab}
        dashboardJson={result?.output ?? null}
        outputType={outputType}
        metrics={metrics}
        review={review}
        agentLogs={agentLogs}
        selectedAgentRole={selectedAgentRole}
        selectedPipeline={selectedPipeline}
        agentConfigs={agentConfigs}
      />
    </div>
  )
}