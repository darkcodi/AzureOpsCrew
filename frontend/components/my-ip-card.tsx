"use client"

import { useState, useCallback, useEffect, type CSSProperties } from "react"
import {
  Globe,
  MapPin,
  Server,
  Copy,
  Check,
  RefreshCw,
  Shield,
  Wifi,
  AlertTriangle,
  Clock,
} from "lucide-react"

// Backend IP data structure
export interface IpInfo {
  ipVersion?: number
  ipAddress?: string
  latitude?: number
  longitude?: number
  countryName?: string
  countryCode?: string
  capital?: string
  phoneCodes?: number[]
  timeZones?: string[]
  zipCode?: string
  cityName?: string
  regionName?: string
  regionCode?: string
  continent?: string
  continentCode?: string
  currencies?: string[]
  languages?: string[]
  asn?: string
  asnOrganization?: string
  isProxy?: boolean
}

interface MyIpCardProps {
  ipInfo?: IpInfo
  onRefresh?: () => void
  onFollowUp?: (message: string) => void
}

const cardStyle: CSSProperties = {
  background: "linear-gradient(135deg, rgba(30, 31, 35, 0.95), rgba(40, 41, 46, 0.95))",
  border: "1px solid rgba(255, 255, 255, 0.08)",
  borderRadius: 12,
  padding: 16,
  marginTop: 8,
  marginBottom: 8,
  maxWidth: 560,
  fontSize: 13,
  color: "#dcddde",
  fontFamily: "inherit",
}

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  marginBottom: 12,
}

const titleStyle: CSSProperties = {
  fontSize: 15,
  fontWeight: 600,
  color: "#ffffff",
  margin: 0,
}

function ActionBtn({
  label,
  icon,
  onClick,
  color = "#0078d4",
  small = false,
  disabled = false,
}: {
  label: string
  icon?: React.ReactNode
  onClick: () => void
  color?: string
  small?: boolean
  disabled?: boolean
}) {
  const [hov, setHov] = useState(false)
  return (
    <button
      disabled={disabled}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
      onMouseEnter={() => setHov(true)}
      onMouseLeave={() => setHov(false)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: small ? "2px 8px" : "4px 10px",
        borderRadius: 6,
        fontSize: small ? 10 : 11,
        fontWeight: 600,
        border: `1px solid ${color}55`,
        color,
        backgroundColor: hov && !disabled ? `${color}22` : "transparent",
        cursor: disabled ? "default" : "pointer",
        opacity: disabled ? 0.5 : 1,
        transition: "background-color 0.15s, opacity 0.15s",
        whiteSpace: "nowrap",
      }}
    >
      {icon} {label}
    </button>
  )
}

function CopyBtn({ text, label, id }: { text: string; label?: string; id: string }) {
  const [copied, setCopied] = useState<string | null>(null)

  const copy = useCallback(() => {
    navigator.clipboard.writeText(text).catch(() => {})
    setCopied(id)
    setTimeout(() => setCopied(null), 1500)
  }, [text, id])

  return (
    <ActionBtn
      label={copied === id ? "Copied!" : label ?? "Copy"}
      icon={copied === id ? <Check size={10} /> : <Copy size={10} />}
      onClick={copy}
      color={copied === id ? "#43b581" : "#99aab5"}
      small
    />
  )
}

function InfoRow({
  icon,
  label,
  value,
  valueColor,
}: {
  icon: React.ReactNode
  label: string
  value: string
  valueColor?: string
}) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        padding: "8px 12px",
        borderRadius: 8,
        backgroundColor: "rgba(255,255,255,0.03)",
        border: "1px solid rgba(255,255,255,0.05)",
      }}
    >
      <div style={{ color: "#0078d4", display: "flex", alignItems: "center" }}>
        {icon}
      </div>
      <span style={{ color: "#99aab5", fontSize: 12, flex: 1 }}>{label}</span>
      <span
        style={{
          fontSize: 13,
          fontWeight: 500,
          color: valueColor || "#ffffff",
          fontFamily: "monospace",
        }}
      >
        {value || "N/A"}
      </span>
    </div>
  )
}

function SecurityBadge({
  isProxy,
}: {
  isProxy?: boolean
}) {
  if (isProxy) {
    return (
      <span
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: 4,
          padding: "2px 8px",
          borderRadius: 12,
          fontSize: 10,
          fontWeight: 600,
          backgroundColor: "rgba(250, 166, 26, 0.2)",
          color: "#faa61a",
          border: "1px solid rgba(250, 166, 26, 0.4)",
        }}
      >
        <AlertTriangle size={10} /> Proxy Detected
      </span>
    )
  }
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: "2px 8px",
        borderRadius: 12,
        fontSize: 10,
        fontWeight: 600,
        backgroundColor: "rgba(67, 181, 129, 0.2)",
        color: "#43b581",
        border: "1px solid rgba(67, 181, 129, 0.4)",
      }}
    >
      <Shield size={10} /> Direct Connection
    </span>
  )
}

export function MyIpCard({ ipInfo, onRefresh, onFollowUp }: MyIpCardProps) {
  const [isRefreshing, setIsRefreshing] = useState(false)

  const handleFollowUp = useCallback(
    (message: string) => {
      if (onFollowUp) {
        onFollowUp(message)
      }
    },
    [onFollowUp]
  )

  const handleRefresh = useCallback(() => {
    if (onRefresh) {
      setIsRefreshing(true)
      onRefresh()
      setTimeout(() => setIsRefreshing(false), 1000)
    }
  }, [onRefresh])

  const location = [ipInfo?.cityName, ipInfo?.regionName, ipInfo?.countryName]
    .filter(Boolean)
    .join(", ")

  // Get first timezone or default
  const timezone = ipInfo?.timeZones?.[0] || null

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={titleStyle}>My IP Information</h3>
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <SecurityBadge isProxy={ipInfo?.isProxy} />
          <button
            onClick={handleRefresh}
            disabled={isRefreshing}
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              padding: "4px",
              borderRadius: 6,
              border: "1px solid rgba(255,255,255,0.1)",
              backgroundColor: "transparent",
              color: "#99aab5",
              cursor: isRefreshing ? "default" : "pointer",
              opacity: isRefreshing ? 0.5 : 1,
              transition: "all 0.15s",
            }}
            onMouseEnter={(e) => {
              if (!isRefreshing) {
                e.currentTarget.style.backgroundColor = "rgba(255,255,255,0.1)"
              }
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = "transparent"
            }}
          >
            <RefreshCw
              size={14}
              style={isRefreshing ? { animation: "spin 1s linear infinite" } : {}}
            />
          </button>
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {/* Public IP */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            padding: "12px",
            borderRadius: 8,
            backgroundColor: "rgba(0,120,212,0.1)",
            border: "1px solid rgba(0,120,212,0.3)",
          }}
        >
          <Globe size={18} color="#0078d4" />
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 11, color: "#99aab5", marginBottom: 2 }}>
              Public IP Address
            </div>
            <div
              style={{
                fontSize: 16,
                fontWeight: 600,
                color: "#ffffff",
                fontFamily: "monospace",
              }}
            >
              {ipInfo?.ipAddress || "Unknown"}
            </div>
          </div>
          <CopyBtn text={ipInfo?.ipAddress || ""} label="Copy" id="public-ip" />
        </div>

        {/* Info Grid */}
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {ipInfo?.ipVersion && (
            <InfoRow
              icon={<Server size={14} />}
              label="IP Version"
              value={`IPv${ipInfo.ipVersion}`}
            />
          )}

          {location && (
            <InfoRow
              icon={<MapPin size={14} />}
              label="Location"
              value={location}
            />
          )}

          {ipInfo?.asnOrganization && (
            <InfoRow
              icon={<Wifi size={14} />}
              label="ISP / Organization"
              value={ipInfo.asnOrganization}
            />
          )}

          {ipInfo?.asn && (
            <InfoRow icon={<Server size={14} />} label="ASN" value={ipInfo.asn} />
          )}

          {timezone && (
            <InfoRow
              icon={<Clock size={14} />}
              label="Timezone"
              value={timezone}
            />
          )}

          {ipInfo?.zipCode && (
            <InfoRow icon={<MapPin size={14} />} label="ZIP Code" value={ipInfo.zipCode} />
          )}

          {ipInfo?.countryCode && (
            <InfoRow
              icon={<Globe size={14} />}
              label="Country Code"
              value={ipInfo.countryCode}
            />
          )}
        </div>
      </div>

      {/* Action Buttons */}
      <div style={{ display: "flex", gap: 6, marginTop: 12, flexWrap: "wrap" }}>
        <ActionBtn
          label="IP Details"
          icon={<Server size={11} />}
          onClick={() =>
            handleFollowUp(
              `Tell me more about my IP address ${ipInfo?.ipAddress}. What information is exposed and how can I protect my privacy?`
            )
          }
        />
        <ActionBtn
          label="Security Check"
          icon={<Shield size={11} />}
          onClick={() =>
            handleFollowUp(
              `Perform a security check for IP ${ipInfo?.ipAddress}. Check for any known threats, leaks, or vulnerabilities.`
            )
          }
          color="#faa61a"
        />
        <ActionBtn
          label="Speed Test"
          icon={<RefreshCw size={11} />}
          onClick={() =>
            handleFollowUp("Run a network speed test and analyze my connection quality.")
          }
          color="#43b581"
        />
      </div>
    </div>
  )
}
