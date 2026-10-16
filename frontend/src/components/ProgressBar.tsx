interface Props {
  agents: Array<{ role: string }>
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

export function ProgressBar({ agents, statuses }: Props) {
  return (
    <div className="progress-section">
      <div className="progress-bar">
        {agents.map(agent => {
          const status = statuses[agent.role] ?? 'idle'
          const cls = agentClass(agent.role)
          let stateClass = ''
          if (status === 'done') stateClass = 'done'
          else if (status === 'running') stateClass = 'active'
          else if (status === 'skipped') stateClass = 'skipped'
          else if (status === 'error') stateClass = 'done'

          return (
            <div
              key={agent.role}
              className={`progress-segment ${stateClass} ${cls}`}
            />
          )
        })}
      </div>
    </div>
  )
}
