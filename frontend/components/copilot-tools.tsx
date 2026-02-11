"use client"

import { useRenderToolCall, useFrontendTool } from "@copilotkit/react-core"
import {
  BarChart3,
  CheckCircle2,
  Clock,
  FileText,
  Loader2,
  AlertTriangle,
} from "lucide-react"

/**
 * Generative UI tool renders - these let the AI agent render
 * rich UI components directly in the CopilotKit chat.
 */
export function CopilotGenerativeTools() {
  // Render-only: show a status card
  useRenderToolCall({
    name: "showStatusCard",
    description:
      "Display a status card with title, description, and status indicator. Use this to show task progress, system status, or notifications.",
    parameters: [
      {
        name: "title",
        type: "string",
        description: "Title of the status card",
        required: true,
      },
      {
        name: "description",
        type: "string",
        description: "Description or details",
        required: true,
      },
      {
        name: "status",
        type: "string",
        description:
          "Status: 'success', 'warning', 'error', or 'loading'",
        required: true,
      },
    ],
    render: ({ status: toolStatus, args }) => {
      if (toolStatus === "inProgress") {
        return (
          <div
            className="flex items-center gap-2 rounded-lg p-3"
            style={{ backgroundColor: "hsl(228, 6%, 18%)" }}
          >
            <Loader2
              className="h-4 w-4 animate-spin"
              style={{ color: "hsl(235, 86%, 65%)" }}
            />
            <span
              className="text-sm"
              style={{ color: "hsl(210, 3%, 80%)" }}
            >
              Loading status...
            </span>
          </div>
        )
      }
      const { title, description, status: cardStatus } = args
      const icons: Record<string, typeof CheckCircle2> = {
        success: CheckCircle2,
        warning: AlertTriangle,
        error: AlertTriangle,
        loading: Clock,
      }
      const colors: Record<string, string> = {
        success: "hsl(145, 65%, 45%)",
        warning: "hsl(40, 85%, 55%)",
        error: "hsl(0, 70%, 55%)",
        loading: "hsl(235, 86%, 65%)",
      }
      const Icon = icons[cardStatus] ?? CheckCircle2
      const color = colors[cardStatus] ?? "hsl(235, 86%, 65%)"

      return (
        <div
          className="my-2 rounded-lg p-4"
          style={{
            backgroundColor: "hsl(228, 6%, 18%)",
            border: `1px solid ${color}33`,
          }}
        >
          <div className="flex items-center gap-2">
            <Icon className="h-5 w-5" style={{ color }} />
            <span
              className="text-sm font-semibold"
              style={{ color: "hsl(0, 0%, 100%)" }}
            >
              {title}
            </span>
          </div>
          <p
            className="mt-2 text-sm"
            style={{ color: "hsl(210, 3%, 80%)" }}
          >
            {description}
          </p>
        </div>
      )
    },
  })

  // Render-only: show data table
  useRenderToolCall({
    name: "showDataTable",
    description:
      "Display data in a formatted table. Use this when presenting structured data, comparison results, or listings.",
    parameters: [
      {
        name: "title",
        type: "string",
        description: "Table title",
        required: true,
      },
      {
        name: "headers",
        type: "string",
        description:
          "Comma-separated column headers, e.g. 'Name,Value,Status'",
        required: true,
      },
      {
        name: "rows",
        type: "string",
        description:
          "Pipe-separated rows, semicolons separate cells. e.g. 'CPU,85%,OK|RAM,60%,OK'",
        required: true,
      },
    ],
    render: ({ status, args }) => {
      if (status === "inProgress") {
        return (
          <div
            className="flex items-center gap-2 rounded-lg p-3"
            style={{ backgroundColor: "hsl(228, 6%, 18%)" }}
          >
            <Loader2
              className="h-4 w-4 animate-spin"
              style={{ color: "hsl(235, 86%, 65%)" }}
            />
            <span
              className="text-sm"
              style={{ color: "hsl(210, 3%, 80%)" }}
            >
              Loading data...
            </span>
          </div>
        )
      }

      const headers = (args.headers ?? "").split(",").map((h: string) => h.trim())
      const rows = (args.rows ?? "").split("|").map((row: string) =>
        row.split(",").map((cell: string) => cell.trim())
      )

      return (
        <div
          className="my-2 overflow-hidden rounded-lg"
          style={{
            backgroundColor: "hsl(228, 6%, 18%)",
            border: "1px solid hsl(228, 6%, 26%)",
          }}
        >
          <div
            className="flex items-center gap-2 px-4 py-2.5"
            style={{ borderBottom: "1px solid hsl(228, 6%, 26%)" }}
          >
            <BarChart3
              className="h-4 w-4"
              style={{ color: "hsl(235, 86%, 65%)" }}
            />
            <span
              className="text-sm font-semibold"
              style={{ color: "hsl(0, 0%, 100%)" }}
            >
              {args.title}
            </span>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr
                style={{
                  borderBottom: "1px solid hsl(228, 6%, 26%)",
                }}
              >
                {headers.map((h: string) => (
                  <th
                    key={h}
                    className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider"
                    style={{ color: "hsl(214, 5%, 55%)" }}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row: string[], i: number) => (
                <tr
                  key={`row-${i}`}
                  style={{
                    borderBottom:
                      i < rows.length - 1
                        ? "1px solid hsl(228, 6%, 24%)"
                        : undefined,
                  }}
                >
                  {row.map((cell: string, j: number) => (
                    <td
                      key={`cell-${i}-${j}`}
                      className="px-4 py-2"
                      style={{ color: "hsl(210, 3%, 85%)" }}
                    >
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )
    },
  })

  // Frontend tool with handler: generate a summary card
  useFrontendTool({
    name: "createTaskList",
    description:
      "Create and display a task list with items and their completion status. Use when discussing tasks, todos, or action items.",
    parameters: [
      {
        name: "title",
        type: "string",
        description: "Title of the task list",
        required: true,
      },
      {
        name: "tasks",
        type: "string",
        description:
          "Pipe-separated tasks, each as 'taskName:done/todo'. e.g. 'Setup repo:done|Write tests:todo|Deploy:todo'",
        required: true,
      },
    ],
    handler: async ({ title, tasks }) => {
      const parsed = (tasks as string).split("|").map((t: string) => {
        const [name, status] = t.split(":")
        return { name: name?.trim(), done: status?.trim() === "done" }
      })
      return `Created task list "${title}" with ${parsed.length} tasks (${parsed.filter((t) => t.done).length} completed).`
    },
    render: ({ status, args }) => {
      if (status === "inProgress") {
        return (
          <div
            className="flex items-center gap-2 rounded-lg p-3"
            style={{ backgroundColor: "hsl(228, 6%, 18%)" }}
          >
            <Loader2
              className="h-4 w-4 animate-spin"
              style={{ color: "hsl(235, 86%, 65%)" }}
            />
            <span
              className="text-sm"
              style={{ color: "hsl(210, 3%, 80%)" }}
            >
              Creating tasks...
            </span>
          </div>
        )
      }

      const parsed = (args.tasks ?? "").split("|").map((t: string) => {
        const [name, st] = t.split(":")
        return { name: name?.trim(), done: st?.trim() === "done" }
      })

      return (
        <div
          className="my-2 rounded-lg p-4"
          style={{
            backgroundColor: "hsl(228, 6%, 18%)",
            border: "1px solid hsl(228, 6%, 26%)",
          }}
        >
          <div className="mb-3 flex items-center gap-2">
            <FileText
              className="h-4 w-4"
              style={{ color: "hsl(235, 86%, 65%)" }}
            />
            <span
              className="text-sm font-semibold"
              style={{ color: "hsl(0, 0%, 100%)" }}
            >
              {args.title}
            </span>
            <span
              className="ml-auto text-xs"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              {parsed.filter((t) => t.done).length}/{parsed.length}
            </span>
          </div>
          <div className="flex flex-col gap-1.5">
            {parsed.map((task) => (
              <div key={task.name} className="flex items-center gap-2">
                <div
                  className="flex h-4 w-4 shrink-0 items-center justify-center rounded"
                  style={{
                    backgroundColor: task.done
                      ? "hsl(145, 65%, 45%)"
                      : "transparent",
                    border: task.done
                      ? "none"
                      : "1.5px solid hsl(214, 5%, 40%)",
                  }}
                >
                  {task.done && (
                    <CheckCircle2 className="h-3 w-3 text-white" />
                  )}
                </div>
                <span
                  className="text-sm"
                  style={{
                    color: task.done
                      ? "hsl(214, 5%, 55%)"
                      : "hsl(210, 3%, 85%)",
                    textDecoration: task.done ? "line-through" : "none",
                  }}
                >
                  {task.name}
                </span>
              </div>
            ))}
          </div>
        </div>
      )
    },
  })

  return null
}
