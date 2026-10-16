import { useEffect, useRef } from 'react'

interface LogEntry {
  time: string
  agent: string
  message: string
  level: string
}

function agentClass(agent: string): string {
  const a = agent.toLowerCase()
  if (a.includes('extract')) return 'extractor'
  if (a.includes('archit')) return 'architect'
  if (a.includes('review')) return 'reviewer'
  if (a.includes('valid')) return 'validator'
  return 'system'
}

export function LogViewer({ logs }: { logs: LogEntry[] }) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs.length])

  return (
    <div className="logs-area">
      {logs.length === 0 ? (
        <div className="logs-empty">
          Waiting for a run...
        </div>
      ) : (
        logs.map((log, i) => (
          <div key={i} className="log-line">
            <span className="log-time">{log.time}</span>
            <span className={`log-agent ${agentClass(log.agent)}`}>
              {log.agent}
            </span>
            <span className={`log-msg ${log.level === 'error' ? 'error' : ''}`}>
              {log.message}
            </span>
          </div>
        ))
      )}
      <div ref={bottomRef} />
    </div>
  )
}
