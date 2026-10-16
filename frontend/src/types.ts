export type AgentType = 'generate' | 'review' | 'check'

export const AGENT_TYPES: { value: AgentType; label: string }[] = [
  { value: 'generate', label: 'Generate (produces the result)' },
  { value: 'review', label: 'Review (checks the result, can send it back)' },
  { value: 'check', label: 'Check (built-in validation in code, no prompt)' },
]

export interface AgentInfo {
  role: string
  name: string
  description: string
  step: number
  promptFile: string
  type: AgentType
  checker?: string
}

export interface PipelineInfo {
  name: string
  displayName: string
  description: string
  outputType: 'json' | 'html' | 'csv' | 'text' | 'markdown'
  agents: AgentInfo[]
  maxCorrectionLoops: number
}

export interface AgentConfig {
  role: string
  model: string
  enabled: boolean
  provider?: string 
}

export interface ModelInfo {
  id: string
  name: string
  provider?: string 
}

export interface PipelineEvent {
  type: string
  agent?: string
  step?: number
  message?: string
  data?: unknown
  timestamp: string
}

export interface ReviewIssue {
  severity: string
  description: string
}

export interface ReviewResult {
  issues: ReviewIssue[]
  suggestions: string[]
}

export interface PipelineStats {
  totalDurationMs: number
  metricsCount: number
  outputSize: number 
  correctionLoops: number
  agents: Record<string, { status: string; durationMs?: number; [key: string]: unknown }>
}

export interface CompletionData {
   output: string 
  stats: PipelineStats
}

export interface Metric {
  name: string
  type: string
}

export type AppState = 'idle' | 'running' | 'paused' | 'complete' | 'error'

export type OutputType = 'json' | 'html' | 'csv' | 'text' | 'markdown'
