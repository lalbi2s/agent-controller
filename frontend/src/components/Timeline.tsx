interface AgentTiming {
  startMs: number
  endMs?: number
}

interface Props {
  agents: Array<{ role: string; name: string }>
  timings: Record<string, AgentTiming>
  statuses: Record<string, string>
}

function agentClass(role: string): string {
  const r = role.toLowerCase()
  if (r.includes('extract')) return 'extractor'
  if (r.includes('archit')) return 'architect'
  if (r.includes('review')) return 'reviewer'
  if (r.includes('valid')) return 'validator'
  return ''
}

export function Timeline({ agents, timings, statuses }: Props) {
  const durations = agents.map(a => {
    const t = timings[a.role]
    if (!t?.endMs) return 0
    return t.endMs - t.startMs
  })
  const maxDuration = Math.max(...durations, 1)

  return (
    <div className="timeline-section">
      {agents.map(agent => {
        const cls = agentClass(agent.role)
        const t = timings[agent.role]
        const status = statuses[agent.role] ?? 'idle'
        const durationMs = t?.endMs ? t.endMs - t.startMs : 0
        const pct = durationMs > 0 ? (durationMs / maxDuration) * 100 : 0
        const isRunning = status === 'running'
        const sec = durationMs > 0 ? (durationMs / 1000).toFixed(1) : '—'

        return (
          <div key={agent.role} className="timeline-row">
            <span className={`timeline-label ${cls}`}>{agent.name}</span>
            <div className="timeline-bar-track">
              <div
                className={`timeline-bar-fill ${cls}`}
                style={{
                  width: isRunning ? '40%' : `${pct}%`,
                  opacity: isRunning ? 0.5 : 1,
                  animation: isRunning ? 'progress-pulse 1s infinite' : 'none'
                }}
              />
            </div>
            <span className="timeline-time">{isRunning ? '...' : sec}</span>
          </div>
        )
      })}
    </div>
  )
}
