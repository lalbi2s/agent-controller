export interface AgentInfo {
  role: string
  name: string
  description: string
  step: number
  promptFile: string
  outputBehavior: string      
  inputSource: string         
  correctionTarget?: string  
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
  promptFile: string | null
  provider?: string 
}

export interface ModelInfo {
  id: string
  name: string
  tier: string
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
  imported?: boolean
  dynamic?: boolean
}

export type AppState = 'idle' | 'running' | 'paused' | 'complete' | 'error'

export type OutputType = 'json' | 'html' | 'csv' | 'text' | 'markdown'
