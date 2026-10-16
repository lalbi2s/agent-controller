import { useState } from "react";
import {
  Database,
  Layout,
  SearchCheck,
  ShieldCheck,
  Hexagon,
  Circle,
  Plus,
  Trash2,
  Pencil,
  KeyRound,
} from "lucide-react";
import type { PipelineInfo, AgentConfig, ModelInfo } from "../types";
import { PipelineCreator } from "./PipelineCreator";
import { ApiKeysPage } from "./ApiKeysPage";
import { PipelineEditor } from "./PipelineEditor";

interface HistoryEntry {
  id: string;
  filename: string;
  status: "complete" | "error";
  time: string;
}

interface Props {
  pipelines: PipelineInfo[];
  pipelinesLoading: boolean;
  selectedPipeline: PipelineInfo | null;
  onPipelineChange: (name: string) => void;
  onPipelineCreated: () => void;
  onPipelineDelete: (name: string) => void;
  agentConfigs: Record<string, AgentConfig>;
  models: ModelInfo[];
  onAgentConfigChange: (role: string, update: Partial<AgentConfig>) => void;
  agentStatuses: Record<string, string>;
  agentTimings: Record<string, { startMs: number; endMs?: number }>;
  disabled: boolean;
  breakpoints: Record<string, boolean>;
  onBreakpointToggle: (role: string) => void;
  selectedAgentRole: string | null;
  onAgentSelect: (role: string) => void;
  history: HistoryEntry[];
}

const PROVIDERS = ["anthropic", "openai"] as const;

function agentColor(role: string): string {
  const r = role.toLowerCase();
  if (r.includes("extract")) return "var(--agent-extractor)";
  if (r.includes("archit")) return "var(--agent-architect)";
  if (r.includes("review")) return "var(--agent-reviewer)";
  return "var(--agent-validator)";
}

function getAgentIcon(role: string, status: string, color: string) {
  const isRunning = status === "running";
  const isIdle = status === "idle";

  const props = {
    size: 16,
    strokeWidth: 1.5,
    color: isIdle ? "var(--text-muted)" : color,
    style: {
      flexShrink: 0,
      transition: "all 0.3s ease",
      opacity: isIdle ? 0.5 : 1,
      animation: isRunning ? "icon-pulse 1.5s ease-in-out infinite" : "none",
    },
  };

  const r = role.toLowerCase();
  if (r.includes("extract")) return <Database {...props} />;
  if (r.includes("archit")) return <Layout {...props} />;
  if (r.includes("review")) return <SearchCheck {...props} />;
  return <ShieldCheck {...props} />;
}

export function Sidebar({
  pipelines,
  pipelinesLoading,
  selectedPipeline,
  onPipelineChange,
  onPipelineCreated,
  onPipelineDelete,
  agentConfigs,
  models,
  onAgentConfigChange,
  agentStatuses,
  agentTimings,
  disabled,
  breakpoints,
  onBreakpointToggle,
  selectedAgentRole,
  onAgentSelect,
  history,
}: Props) {
  const [showCreator, setShowCreator] = useState(false);
  const [showEditor, setShowEditor] = useState(false);
  const [showKeys, setShowKeys] = useState(false);

  const handleProviderChange = (role: string, provider: string) => {
    const firstModel = models.find((m) => m.provider === provider);
    onAgentConfigChange(role, {
      provider,
      model: firstModel?.id ?? "",
    });
  };

  return (
    <aside className="sidebar">
      <style>{`
        @keyframes icon-pulse {
          0%, 100% { opacity: 1; transform: scale(1); }
          50% { opacity: 0.4; transform: scale(1.1); }
        }
      `}</style>

      {/* Dialog modals - rendered via Portal, appear over everything */}
      {showCreator && (
        <PipelineCreator
          onClose={() => setShowCreator(false)}
          onCreated={() => {
            setShowCreator(false);
            onPipelineCreated();
          }}
        />
      )}

      {showKeys && <ApiKeysPage onClose={() => setShowKeys(false)} />}

      {showEditor && selectedPipeline && (
        <PipelineEditor
          pipeline={selectedPipeline}
          onClose={() => setShowEditor(false)}
          onSaved={() => {
            setShowEditor(false);
            onPipelineCreated();
          }}
        />
      )}

      {/* Logo */}
      <div className="sidebar-logo">
        <div className="logo-icon">
          <Hexagon size={22} strokeWidth={1.5} color="var(--text)" />
        </div>
        <span>Agent-Controller</span>
        <button
          className="pc-btn-icon"
          style={{ marginLeft: "auto" }}
          onClick={() => setShowKeys(true)}
          title="API keys"
        >
          <KeyRound size={13} strokeWidth={2} />
        </button>
      </div>

      {/* Pipeline selector */}
      <div className="sidebar-section">
        <div className="sidebar-section-label">Pipeline</div>
        <div className="pipeline-selector-row">
          <div className="select-wrap" style={{ flex: 1 }}>
            <select
              value={selectedPipeline?.name ?? ""}
              onChange={(e) => onPipelineChange(e.target.value)}
              disabled={disabled}
            >
              <option value="" disabled>
                {pipelinesLoading ? "Loading..." : "Select..."}
              </option>
              {pipelines.map((p) => (
                <option key={p.name} value={p.name}>
                  {p.displayName}
                </option>
              ))}
            </select>
          </div>
          <button
            className="pc-btn-icon"
            onClick={() => setShowCreator(true)}
            disabled={disabled}
            title="New pipeline"
          >
            <Plus size={14} strokeWidth={2} />
          </button>
          {selectedPipeline && (
            <>
              <button
                className="pc-btn-icon"
                onClick={() => setShowEditor(true)}
                disabled={disabled}
                title="Edit pipeline"
              >
                <Pencil size={13} strokeWidth={2} />
              </button>
              <button
                className="pc-btn-icon pc-btn-danger"
                onClick={() => {
                  if (confirm(`Delete pipeline "${selectedPipeline.displayName}"?`))
                    onPipelineDelete(selectedPipeline.name);
                }}
                disabled={disabled}
                title="Delete pipeline"
              >
                <Trash2 size={13} strokeWidth={2} />
              </button>
            </>
          )}
        </div>
      </div>

      {/* Agents - always visible */}
      <div className="sidebar-section-label" style={{ padding: "0 14px" }}>
        Agents
      </div>
      <div className="agent-list">
        {selectedPipeline?.agents.map((agent) => {
          const config = agentConfigs[agent.role];
          if (!config) return null;
          const status = agentStatuses[agent.role] ?? "idle";
          const timing = agentTimings[agent.role];
          const durationMs = timing?.endMs ? timing.endMs - timing.startMs : 0;
          const color = agentColor(agent.role);
          const activeProvider = config.provider ?? "anthropic";

          return (
            <div
              key={agent.role}
              className={`agent-card ${!config.enabled ? "disabled" : ""} ${
                selectedAgentRole === agent.role ? "selected" : ""
              }`}
              onClick={() => onAgentSelect(agent.role)}
              style={{ cursor: "pointer" }}
            >
              <div className="agent-card-top">
                <div className="agent-card-left">
                  {getAgentIcon(agent.role, status, color)}
                  <span className="agent-card-name" style={{ marginLeft: "4px" }}>
                    {agent.name}
                  </span>
                  {status === "running" && <span className="agent-status-dot running" />}
                  {status === "done" && durationMs > 0 && (
                    <span className="agent-card-time">{(durationMs / 1000).toFixed(1)}s</span>
                  )}
                </div>

                <div
                  style={{ display: "flex", alignItems: "center", gap: "6px" }}
                  onClick={(e) => e.stopPropagation()}
                >
                  <button
                    className={`btn-breakpoint ${breakpoints[agent.role] ? "active" : ""}`}
                    onClick={() => onBreakpointToggle(agent.role)}
                    title={breakpoints[agent.role] ? "Remove breakpoint" : "Add breakpoint"}
                  >
                    <Circle
                      size={10}
                      fill={breakpoints[agent.role] ? "#f59e0b" : "none"}
                      strokeWidth={1.5}
                      color={breakpoints[agent.role] ? "#f59e0b" : "var(--text-muted)"}
                    />
                  </button>
                  <label className="toggle">
                    <input
                      type="checkbox"
                      checked={config.enabled}
                      onChange={(e) =>
                        onAgentConfigChange(agent.role, { enabled: e.target.checked })
                      }
                      disabled={disabled}
                    />
                    <span className="toggle-track" />
                  </label>
                </div>
              </div>

              {config.enabled && agent.type !== "check" && (
                <div className="agent-card-controls" onClick={(e) => e.stopPropagation()}>
                  <div className="select-wrap">
                    <select
                      value={activeProvider}
                      onChange={(e) => handleProviderChange(agent.role, e.target.value)}
                      disabled={disabled}
                    >
                      {PROVIDERS.map((p) => (
                        <option key={p} value={p}>{p}</option>
                      ))}
                    </select>
                  </div>
                  <div className="select-wrap">
                    <select
                      value={config.model}
                      onChange={(e) =>
                        onAgentConfigChange(agent.role, { model: e.target.value })
                      }
                      disabled={disabled}
                    >
                      {models
                        .filter((m) => m.provider === activeProvider)
                        .map((m) => (
                          <option key={m.id} value={m.id}>{m.name}</option>
                        ))}
                    </select>
                  </div>
                </div>
              )}
            </div>
          );
        })}

        {!selectedPipeline && (
          <div style={{ padding: "20px 14px", color: "var(--text-muted)", fontSize: "10px", textAlign: "center" }}>
            Select a pipeline
          </div>
        )}
      </div>

      {/* History */}
      {history.length > 0 && (
        <div className="sidebar-history">
          <div className="sidebar-section-label">History</div>
          {history.map((h) => (
            <div key={h.id} className="history-item">
              <span className="history-item-name">{h.filename}</span>
              <span
                className="history-item-status"
                style={{
                  width: "6px",
                  height: "6px",
                  borderRadius: "50%",
                  flexShrink: 0,
                  background: h.status === "complete" ? "var(--success)" : "var(--error)",
                }}
              />
            </div>
          ))}
        </div>
      )}
    </aside>
  );
}
