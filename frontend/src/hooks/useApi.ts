import { useState, useEffect, useCallback } from 'react'
import type { PipelineInfo, ModelInfo } from '../types'

export function usePipelines() {
  const [pipelines, setPipelines] = useState<PipelineInfo[]>([])
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(() => {
    fetch('/api/pipelines')
      .then(r => r.json())
      .then(setPipelines)
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { refresh() }, [refresh])

  return { pipelines, loading, refresh }
}

export function useModels() {
  const [models, setModels] = useState<ModelInfo[]>([])

  useEffect(() => {
    fetch('/api/models')
      .then(r => r.json())
      .then(setModels)
      .catch(console.error)
  }, [])

  return models
}

export function useAgentDefaults() {
  const [defaults, setDefaults] = useState<Record<string, { provider: string; model: string }>>({})

  useEffect(() => {
    fetch('/api/agent-defaults')
      .then(r => r.json())
      .then(setDefaults)
      .catch(console.error)
  }, [])

  return defaults
}