import { toast } from "@/hooks/use-toast"

const DEFAULT_TIMEOUT = 30000 // 30 seconds

interface RequestOptions extends RequestInit {
  timeout?: number
}

/**
 * Wrapper around fetch that shows error toasts for 5xx errors and timeouts.
 * Use this instead of native fetch for all API calls.
 */
export async function fetchWithErrorHandling(
  url: string,
  options: RequestOptions = {}
): Promise<Response> {
  const { timeout = DEFAULT_TIMEOUT, ...fetchOptions } = options

  // Create abort controller for timeout
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), timeout)

  const startTime = performance.now()
  let response: Response | undefined

  try {
    response = await fetch(url, {
      ...fetchOptions,
      signal: controller.signal,
    })

    const duration = Math.round(performance.now() - startTime)

    // Show toast for 5xx errors
    if (response.status >= 500 && response.status < 600) {
      toast({
        variant: "destructive",
        title: `Server error ${response.status}`,
        description: `${fetchOptions.method || "GET"} ${url} — ${duration}ms`,
      })
    }

    return response
  } catch (error) {
    const duration = Math.round(performance.now() - startTime)

    // Handle timeout
    if (error instanceof Error && error.name === "AbortError") {
      toast({
        variant: "destructive",
        title: "Request timeout",
        description: `${fetchOptions.method || "GET"} ${url} — exceeded ${timeout}ms`,
      })
      throw new Error(`Request to ${url} timed out after ${timeout}ms`)
    }

    // Handle network errors
    toast({
      variant: "destructive",
      title: "Network error",
      description: `${fetchOptions.method || "GET"} ${url} — ${duration}ms`,
    })
    throw error
  } finally {
    clearTimeout(timeoutId)
  }
}
