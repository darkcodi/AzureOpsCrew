"use client"

import { useState, useCallback, type CSSProperties } from "react"
import { Workflow, ChevronDown, ChevronUp, Copy, Check } from "lucide-react"

export interface BackendToolCardData {
  toolCallId: string
  toolName: string
  serverName?: string
  args: Record<string, unknown>
  result: Record<string, unknown>
}

interface BackendToolCardProps {
  toolName: string
  serverName?: string
  args: Record<string, unknown>
  result: Record<string, unknown>
}

function tryPrettyJson(obj: Record<string, unknown>): string {
  try {
    return JSON.stringify(obj, null, 2)
  } catch {
    return String(obj)
  }
}

/** If result is { SerializedResult: "<json string>", ... }, unwrap and return parsed object for display; else return result as-is. */
function unwrapSerializedResult(result: Record<string, unknown>): Record<string, unknown> {
  const raw = result.SerializedResult
  if (typeof raw !== "string" || !raw.trim()) return result
  try {
    const parsed = JSON.parse(raw) as unknown
    if (parsed != null && typeof parsed === "object" && !Array.isArray(parsed))
      return parsed as Record<string, unknown>
  } catch {
    // fall through
  }
  return result
}

const cardStyle: CSSProperties = {
  backgroundColor: "hsl(228, 6%, 22%)",
  border: "1px solid hsl(228, 6%, 28%)",
  borderRadius: 12,
  padding: 16,
  marginTop: 8,
  marginBottom: 8,
  maxWidth: 560,
  fontSize: 13,
  fontFamily: "inherit",
}

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  marginBottom: 12,
  gap: 8,
}

const titleStyle: CSSProperties = {
  fontSize: 14,
  fontWeight: 600,
  color: "hsl(0, 0%, 100%)",
  margin: 0,
  flex: 1,
  display: "flex",
  alignItems: "center",
  gap: 8,
}

const paramKeyStyle: CSSProperties = {
  color: "hsl(214, 5%, 55%)",
  marginRight: 8,
}

const paramValueStyle: CSSProperties = {
  color: "hsl(0, 0%, 100%)",
}

const sectionLabelStyle: CSSProperties = {
  fontSize: 12,
  fontWeight: 600,
  color: "hsl(214, 5%, 55%)",
  marginBottom: 6,
  marginTop: 0,
}

const outputBlockStyle: CSSProperties = {
  backgroundColor: "hsl(228, 7%, 18%)",
  borderRadius: 8,
  padding: 12,
  marginTop: 12,
  fontFamily: "ui-monospace, monospace",
  fontSize: 12,
  color: "hsl(210, 3%, 92%)",
  overflowX: "auto",
  whiteSpace: "pre-wrap",
  wordBreak: "break-all",
}

export function BackendToolCard({
  toolName,
  serverName,
  args,
  result,
}: BackendToolCardProps) {
  const [expanded, setExpanded] = useState(false)
  const [resultCopied, setResultCopied] = useState(false)
  const displayName = toolName
  const resultToShow = unwrapSerializedResult(result)
  const displayResult = tryPrettyJson(resultToShow)
  const argEntries = Object.entries(args).filter(
    ([_, v]) => v !== undefined && v !== null
  )

  const copyResult = useCallback(() => {
    const text = Object.keys(resultToShow).length > 0 ? displayResult : "<empty>"
    navigator.clipboard.writeText(text).catch(() => {})
    setResultCopied(true)
    setTimeout(() => setResultCopied(false), 1500)
  }, [displayResult, resultToShow])

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <div style={titleStyle}>
          <Workflow size={16} style={{ flexShrink: 0, color: "hsl(214, 5%, 55%)" }} />
          <span>
            Tool call: {displayName}
          </span>
        </div>
        <button
          type="button"
          onClick={() => setExpanded((e) => !e)}
          aria-label={expanded ? "Collapse" : "Expand"}
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            padding: 4,
            background: "transparent",
            border: "none",
            cursor: "pointer",
            color: "hsl(214, 5%, 55%)",
          }}
        >
          {expanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
        </button>
      </div>

      {expanded && (
        <>
          <div style={{ marginBottom: 12 }}>
            <div style={sectionLabelStyle}>Arguments:</div>
            {argEntries.length > 0 ? (
              argEntries.map(([key, value]) => (
                <div
                  key={key}
                  style={{
                    display: "flex",
                    alignItems: "baseline",
                    marginBottom: 4,
                    flexWrap: "wrap",
                    gap: "4px 8px",
                  }}
                >
                  <span style={paramKeyStyle}>{key}</span>
                  <span style={paramValueStyle}>
                    {typeof value === "object"
                      ? JSON.stringify(value)
                      : String(value)}
                  </span>
                </div>
              ))
            ) : (
              <span style={{ ...paramValueStyle, fontSize: 12 }}>—</span>
            )}
          </div>

          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
              <div style={sectionLabelStyle}>Result:</div>
              <button
                type="button"
                onClick={copyResult}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 4,
                  padding: "4px 8px",
                  fontSize: 11,
                  fontWeight: 600,
                  border: "1px solid hsl(214, 5%, 55%)",
                  color: resultCopied ? "hsl(150, 50%, 45%)" : "hsl(214, 5%, 65%)",
                  background: "transparent",
                  borderRadius: 6,
                  cursor: "pointer",
                }}
              >
                {resultCopied ? <Check size={12} /> : <Copy size={12} />}
                {resultCopied ? "Copied!" : "Copy"}
              </button>
            </div>
            <div style={outputBlockStyle}>
              {Object.keys(resultToShow).length > 0 ? (displayResult || "<empty>") : "<empty>"}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
